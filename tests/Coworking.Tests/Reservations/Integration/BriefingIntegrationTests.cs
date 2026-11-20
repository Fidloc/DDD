using System.Text.Json;
using System.Text.Json.Nodes;
using Briefings.Domain;
using Briefings.Integration;
using Reservations.Application;
using Reservations.Domain;
using Reservations.Infrastructure;
using Reservations.Integration;
using Xunit;
using static Coworking.Tests.Reservations.Domain.ReservationFixture;
using BriefingActor = Briefings.Domain.Actor;
using ReservationActor = Reservations.Domain.Actor;
using ReservationDomainException = Reservations.Domain.DomainException;

namespace Coworking.Tests.Reservations.Integration;

public sealed class BriefingIntegrationTests
{
    private static string SampleJson()
    {
        var reservationId = Guid.NewGuid();
        var message = new BriefingRecordedV1
        {
            MessageId = Guid.NewGuid(),
            Version = 1,
            CorrelationId = reservationId,
            ReservationId = reservationId,
            ZoneId = Guid.NewGuid(),
            PersonId = Guid.NewGuid(),
            OfficerId = Guid.NewGuid(),
            Revision = 1,
            OccurredAtUtc = Now.ToUniversalTime(),
            ValidFromUtc = Now.AddDays(-10).ToUniversalTime(),
            ValidUntilUtc = Now.AddDays(355).ToUniversalTime(),
            StateCode = "RECORDED"
        };
        return JsonSerializer.Serialize(message);
    }

    [Fact]
    public void MissingEachRequiredField_RejectsWithoutCacheMutation()
    {
        var json = SampleJson();
        var original = JsonNode.Parse(json)!.AsObject();
        var cache = new BriefingMessageCache();

        foreach (var field in original.Select(x => x.Key).ToArray())
        {
            var copy = JsonNode.Parse(json)!.AsObject();
            copy.Remove(field);

            Assert.Throws<JsonException>(() => cache.Apply(copy.ToJsonString(), Now.AddMinutes(5)));
            Assert.Equal(0, cache.Count);
        }
    }

    [Theory]
    [InlineData("version")]
    [InlineData("state")]
    [InlineData("null")]
    public void InvalidMessage_RejectsWithoutCacheMutation(string kind)
    {
        var data = JsonNode.Parse(SampleJson())!.AsObject();
        if (kind == "version") data["Version"] = 2;
        if (kind == "state") data["StateCode"] = "APPROVED";
        if (kind == "null") data["StateCode"] = null;

        var cache = new BriefingMessageCache();

        Assert.Throws<JsonException>(() => cache.Apply(data.ToJsonString(), Now.AddMinutes(5)));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void RepeatedMessage_WithDifferentOffset_IsDuplicateWithoutNewEntry()
    {
        var jsonUtc = SampleJson();
        var parsed = BriefingContractValidator.Parse(jsonUtc, Now.AddMinutes(5));
        var offset = TimeSpan.FromHours(2);
        var jsonWithOffset = JsonSerializer.Serialize(parsed with
        {
            OccurredAtUtc = parsed.OccurredAtUtc.ToOffset(offset),
            ValidFromUtc = parsed.ValidFromUtc.ToOffset(offset),
            ValidUntilUtc = parsed.ValidUntilUtc.ToOffset(offset)
        });
        var cache = new BriefingMessageCache();

        Assert.Equal(Receipt.Applied, cache.Apply(jsonUtc, Now.AddMinutes(5)));
        Assert.Equal(Receipt.Duplicate, cache.Apply(jsonWithOffset, Now.AddMinutes(5)));
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void SameMessageId_ChangedCanonicalPayload_IsConflict()
    {
        var json = SampleJson();
        var original = BriefingContractValidator.Parse(json, Now.AddMinutes(5));
        var changedJson = JsonSerializer.Serialize(original with
        {
            ValidUntilUtc = original.ValidUntilUtc.AddDays(30)
        });
        var cache = new BriefingMessageCache();

        Assert.Equal(Receipt.Applied, cache.Apply(json, Now.AddMinutes(5)));
        Assert.Throws<InvalidDataException>(() => cache.Apply(changedJson, Now.AddMinutes(5)));
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public async Task ValidBriefingFromSender_IsAppliedAndConfirmsLaboratoryReservation()
    {
        var schedule = Laboratory();
        var reservationId = Submit(schedule, Tomorrow(10, 12), ReservationPurpose.LaboratoryWork);
        var session = new ReservationMemorySession(schedule);

        var officerId = PersonId.New();
        var briefingRecord = new BriefingRecord(new PersonId(OrganizerId.Value), BriefingPolicy.Default);
        briefingRecord.RecordBriefing(
            new BriefingActor(officerId, BriefingRole.BriefingOfficer),
            Now.AddDays(-10),
            Now);

        var publisher = new BriefingNotificationPublisher();
        publisher.Prepare(briefingRecord, reservationId.Value, schedule.ZoneId.Value, officerId.Value, 1, Now);
        var json = publisher.PendingJson();

        var service = new ReservationService(
            session,
            session,
            new StubActorContext(Manager),
            new StubClock(Now.AddMinutes(1)),
            new EmptyBriefingFacts());

        await service.ExecuteIntegrationAsync(
            json,
            BriefingContractValidator.Parse,
            BriefingTranslator.ToLocal,
            m => new ZoneId(m.ZoneId),
            m => new ReservationId(m.ReservationId));

        publisher.Acknowledge(publisher.Pending!.MessageId);

        var saved = await session.FindAsync(schedule.ZoneId);
        Assert.NotNull(saved);
        Assert.Equal(2L, saved.Revision);
        Assert.Equal(ReservationStatus.Confirmed, saved.Aggregate.Get(reservationId).Status);
        Assert.Null(publisher.Pending);
    }

    [Fact]
    public async Task InvalidReservationState_DoesNotCommit()
    {
        var schedule = Laboratory();
        var reservationId = Submit(schedule, Tomorrow(10, 12), ReservationPurpose.LaboratoryWork);
        schedule.Cancel(reservationId, Organizer, Now);
        var session = new ReservationMemorySession(schedule);

        var data = JsonNode.Parse(SampleJson())!.AsObject();
        data["CorrelationId"] = reservationId.Value;
        data["ReservationId"] = reservationId.Value;
        data["ZoneId"] = schedule.ZoneId.Value;
        data["PersonId"] = OrganizerId.Value;

        var service = new ReservationService(
            session,
            session,
            new StubActorContext(Manager),
            new StubClock(Now.AddMinutes(1)),
            new EmptyBriefingFacts());

        await Assert.ThrowsAsync<ReservationDomainException>(() =>
            service.ExecuteIntegrationAsync(
                data.ToJsonString(),
                BriefingContractValidator.Parse,
                BriefingTranslator.ToLocal,
                m => new ZoneId(m.ZoneId),
                m => new ReservationId(m.ReservationId)));

        var after = await session.FindAsync(schedule.ZoneId);
        Assert.NotNull(after);
        Assert.Equal(1L, after.Revision);
        Assert.Equal(ReservationStatus.Cancelled, after.Aggregate.Get(reservationId).Status);
    }

    [Fact]
    public void BoundedContexts_DoNotReferenceEachOther()
    {
        var reservationsRefs = typeof(ZoneSchedule).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        var briefingsRefs = typeof(BriefingRecord).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        var zoneUsageRefs = typeof(global::ZoneUsage.Domain.ZoneUsageRecord).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();

        Assert.DoesNotContain("Briefings.Domain", reservationsRefs);
        Assert.DoesNotContain("ZoneUsage.Domain", reservationsRefs);
        Assert.DoesNotContain("Reservations.Domain", briefingsRefs);
        Assert.DoesNotContain("ZoneUsage.Domain", briefingsRefs);
        Assert.DoesNotContain("Reservations.Domain", zoneUsageRefs);
        Assert.DoesNotContain("Briefings.Domain", zoneUsageRefs);
    }

    private sealed record StubActorContext(ReservationActor CurrentActor) : IActorContext;
    private sealed record StubClock(DateTimeOffset Now) : IClock;
    private sealed class EmptyBriefingFacts : IBriefingFacts
    {
        public Task<BriefingValidity?> GetValidityAsync(UserId organizer) => Task.FromResult<BriefingValidity?>(null);
    }
}
