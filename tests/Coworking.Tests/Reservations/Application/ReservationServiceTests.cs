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
    public async Task ConfirmReservation_ByOrganizerInsteadOfManager_RefusesWithoutStagingOrSaving()
    {
        var schedule = MeetingRoom();
        var reservationId = Submit(schedule, Tomorrow(10, 11));
        var spyRepo = new SpyZoneScheduleRepository(schedule);
        var service = new ReservationService(
            spyRepo, spyRepo,
            new StubActorContext(Organizer),
            new StubClock(Now),
            new StubBriefingFacts(null));

        await Assert.ThrowsAsync<DomainException>(() =>
            service.ConfirmReservationAsync(schedule.ZoneId, reservationId));

        Assert.Equal(0, spyRepo.StagedCount);
        Assert.Equal(0, spyRepo.SavedCount);
    }

    [Fact]
    public async Task ConfirmReservation_WhenUnitOfWorkFails_ThrowsAndKeepsRequestedStatusInStorage()
    {
        var schedule = MeetingRoom();
        var reservationId = Submit(schedule, Tomorrow(10, 11));
        var session = new ReservationMemorySession(schedule);
        var service = new ReservationService(
            session, new FailingUnitOfWork(),
            new StubActorContext(Manager),
            new StubClock(Now),
            new StubBriefingFacts(null));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ConfirmReservationAsync(schedule.ZoneId, reservationId));

        var reloaded = await session.FindAsync(schedule.ZoneId);
        Assert.NotNull(reloaded);
        Assert.Equal(ReservationStatus.Requested, reloaded.Aggregate.Get(reservationId).Status);
        Assert.Equal(1L, reloaded.Revision);
    }

    private sealed record StubActorContext(Actor CurrentActor) : IActorContext;
    private sealed record StubClock(DateTimeOffset Now) : IClock;
    private sealed record StubBriefingFacts(BriefingValidity? Validity) : IBriefingFacts
    {
        public Task<BriefingValidity?> GetValidityAsync(UserId organizer) => Task.FromResult(Validity);
    }

    private sealed class FailingUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync() =>
            throw new InvalidOperationException("Технічний збій збереження розкладу.");
    }

    private sealed class SpyZoneScheduleRepository(ZoneSchedule initial) : IZoneScheduleRepository, IUnitOfWork
    {
        private readonly ZoneScheduleState state = initial.ToState();
        public int StagedCount { get; private set; }
        public int SavedCount { get; private set; }

        public Task<LoadedZoneSchedule?> FindAsync(ZoneId zoneId) =>
            Task.FromResult<LoadedZoneSchedule?>(new LoadedZoneSchedule(ZoneSchedule.Restore(state), 1L));

        public void Stage(ZoneSchedule schedule, long expectedRevision) => StagedCount++;

        public Task SaveChangesAsync()
        {
            SavedCount++;
            return Task.CompletedTask;
        }
    }
}
