using Reservations.Application;
using Reservations.Domain;
using Reservations.Infrastructure;
using Xunit;
using static Coworking.Tests.Reservations.Domain.ReservationFixture;

namespace Coworking.Tests.Reservations.Integration;

public sealed class ReservationStorageTests
{
    [Fact]
    public async Task Save_ThenRestore_ReturnsDistinctInstanceWithoutNewEvents_AndAllowsSubsequentUsage()
    {
        var original = MeetingRoom();
        var slot = Tomorrow(10, 12);
        var reservationId = Submit(original, slot);
        var session = new ReservationMemorySession(original);
        var service = new ReservationService(
            session, session,
            new StubActorContext(Manager),
            new StubClock(Now),
            new StubBriefingFacts(null));

        await service.ConfirmReservationAsync(original.ZoneId, reservationId);

        var loaded = await session.FindAsync(original.ZoneId);
        Assert.NotNull(loaded);
        var restored = loaded.Aggregate;

        Assert.NotSame(original, restored);
        Assert.Equal(original, restored);
        Assert.Equal(ReservationStatus.Confirmed, restored.Get(reservationId).Status);
        Assert.Empty(restored.Events);

        restored.RecordUsageStarted(reservationId, slot.Start);
        Assert.Equal(ReservationStatus.InUse, restored.Get(reservationId).Status);
        Assert.IsType<ReservationTakenIntoUse>(Assert.Single(restored.Events));
    }

    [Fact]
    public async Task SaveChanges_WhenRevisionMismatches_RejectsConcurrentWrite()
    {
        var original = MeetingRoom();
        var firstId = Submit(original, Tomorrow(10, 11));
        var secondId = Submit(original, Tomorrow(12, 13));
        var session = new ReservationMemorySession(original);

        var firstRead = (await session.FindAsync(original.ZoneId))!;
        var secondRead = (await session.FindAsync(original.ZoneId))!;

        firstRead.Aggregate.Confirm(firstId, Manager, null, Now);
        session.Stage(firstRead.Aggregate, firstRead.Revision);
        await session.SaveChangesAsync();

        secondRead.Aggregate.Confirm(secondId, Manager, null, Now);
        session.Stage(secondRead.Aggregate, secondRead.Revision);

        await Assert.ThrowsAsync<DomainException>(() => session.SaveChangesAsync());
    }

    private sealed record StubActorContext(Actor CurrentActor) : IActorContext;
    private sealed record StubClock(DateTimeOffset Now) : IClock;
    private sealed record StubBriefingFacts(BriefingValidity? Validity) : IBriefingFacts
    {
        public Task<BriefingValidity?> GetValidityAsync(UserId organizer) => Task.FromResult(Validity);
    }
}
