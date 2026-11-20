using Xunit;
using ZoneUsage.Application;
using ZoneUsage.Domain;
using ZoneUsage.Infrastructure;

namespace Coworking.Tests.ZoneUsage.Application;

public sealed class ZoneUsageServiceTests
{
    private static readonly DateTimeOffset SlotStart = new(2026, 11, 17, 10, 0, 0, TimeSpan.FromHours(2));
    private static readonly TimeSlot Slot = new(SlotStart, SlotStart.AddHours(2));
    private static readonly Actor Organizer = new(UserId.New(), UsageRole.Organizer);
    private static readonly Actor Administrator = new(UserId.New(), UsageRole.SpaceAdministrator);

    private static ZoneUsageRecord CreateExpected() =>
        ZoneUsageRecord.ExpectFrom(
            new ConfirmedSlot(ReservationId.New(), ZoneId.New(), Organizer.UserId, Slot),
            UsagePolicy.Default);

    [Fact]
    public async Task StartUsage_WithinSlotByOrganizer_StartsAndCommits()
    {
        var usage = CreateExpected();
        var session = new ZoneUsageMemorySession(usage);
        var service = new ZoneUsageService(
            session, session,
            new StubActorContext(Organizer),
            new StubClock(SlotStart.AddMinutes(5)));

        await service.StartUsageAsync(usage.ReservationId);

        var loaded = await session.FindAsync(usage.ReservationId);
        Assert.NotNull(loaded);
        Assert.Equal(2L, loaded.Revision);
        Assert.Equal(UsageStatus.InProgress, loaded.Aggregate.Status);
        Assert.Equal(SlotStart.AddMinutes(5), loaded.Aggregate.StartedAt);
    }

    [Fact]
    public async Task DeclareNoShow_AfterDelayReported_RefusesAndKeepsExpectedState()
    {
        var usage = CreateExpected();
        usage.ReportDelay(Organizer, SlotStart.AddMinutes(10));
        var session = new ZoneUsageMemorySession(usage);
        var service = new ZoneUsageService(
            session, session,
            new StubActorContext(Administrator),
            new StubClock(SlotStart.AddMinutes(30)));

        await Assert.ThrowsAsync<DomainException>(() =>
            service.DeclareNoShowAsync(usage.ReservationId));

        var loaded = await session.FindAsync(usage.ReservationId);
        Assert.NotNull(loaded);
        Assert.Equal(1L, loaded.Revision);
        Assert.Equal(UsageStatus.Expected, loaded.Aggregate.Status);
    }

    private sealed record StubActorContext(Actor CurrentActor) : IActorContext;
    private sealed record StubClock(DateTimeOffset Now) : IClock;
}
