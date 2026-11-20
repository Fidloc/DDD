using Reservations.Application;
using Reservations.Domain;
using Reservations.Infrastructure;
using Xunit;
using static Coworking.Tests.Reservations.Domain.ReservationFixture;

namespace Coworking.Tests.Reservations.Application;

public sealed class ReservationServiceTests
{
    [Fact]
    public async Task ConfirmReservation_ForLaboratoryWithValidBriefing_ConfirmsAndCommits()
    {
        var schedule = Laboratory();
        var slot = Tomorrow(10, 12);
        var reservationId = Submit(schedule, slot, ReservationPurpose.LaboratoryWork);
        var session = new ReservationMemorySession(schedule);
        var validBriefing = new BriefingValidity(OrganizerId, Now.AddMonths(-3), slot.End.AddDays(10));
        var service = new ReservationService(
            session, session,
            new StubActorContext(Manager),
            new StubClock(Now),
            new StubBriefingFacts(validBriefing));

        await service.ConfirmReservationAsync(schedule.ZoneId, reservationId);

        var loaded = await session.FindAsync(schedule.ZoneId);
        Assert.NotNull(loaded);
        Assert.Equal(2L, loaded.Revision);
        Assert.Equal(ReservationStatus.Confirmed, loaded.Aggregate.Get(reservationId).Status);
    }

    [Fact]
    public async Task ConfirmReservation_ByOrganizerInsteadOfManager_RefusesAndKeepsRequestedState()
    {
        var schedule = MeetingRoom();
        var reservationId = Submit(schedule, Tomorrow(10, 11));
        var session = new ReservationMemorySession(schedule);
        var service = new ReservationService(
            session, session,
            new StubActorContext(Organizer),
            new StubClock(Now),
            new StubBriefingFacts(null));

        await Assert.ThrowsAsync<DomainException>(() =>
            service.ConfirmReservationAsync(schedule.ZoneId, reservationId));

        var loaded = await session.FindAsync(schedule.ZoneId);
        Assert.NotNull(loaded);
        Assert.Equal(1L, loaded.Revision);
        Assert.Equal(ReservationStatus.Requested, loaded.Aggregate.Get(reservationId).Status);
    }

    private sealed record StubActorContext(Actor CurrentActor) : IActorContext;
    private sealed record StubClock(DateTimeOffset Now) : IClock;
    private sealed record StubBriefingFacts(BriefingValidity? Validity) : IBriefingFacts
    {
        public Task<BriefingValidity?> GetValidityAsync(UserId organizer) => Task.FromResult(Validity);
    }
}
