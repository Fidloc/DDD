using Reservations.Domain;
using Xunit;
using static Coworking.Tests.Reservations.Domain.ReservationFixture;

namespace Coworking.Tests.Reservations.Domain;

public sealed class ZoneScheduleTests
{
    [Fact]
    public void NormalScenario_SubmitConfirmUseComplete_ReleasesZone()
    {
        var schedule = MeetingRoom();
        var slot = Tomorrow(10, 12);
        var id = Submit(schedule, slot);

        schedule.Confirm(id, Manager, null, Now);
        schedule.RecordUsageStarted(id, slot.Start);
        schedule.RecordUsageCompleted(id, slot.End.AddMinutes(-30));

        Assert.Equal(ReservationStatus.Completed, schedule.Get(id).Status);
        Assert.Equal(
            ["ReservationRequested", "ReservationConfirmed", "ReservationTakenIntoUse", "ZoneReleased"],
            schedule.Events.Select(e => e.GetType().Name).ToArray());
    }

    [Fact]
    public void Confirm_OverlapsConfirmedReservation_RejectsWithConflictReason()
    {
        var schedule = MeetingRoom();
        Confirmed(schedule, Tomorrow(10, 12));
        var urgent = Submit(schedule, Tomorrow(11, 13), ReservationPurpose.Training);

        schedule.Confirm(urgent, Manager, null, Now);

        Assert.Equal(ReservationStatus.Rejected, schedule.Get(urgent).Status);
        Assert.Equal(RejectionCode.Conflict, schedule.Get(urgent).Rejection!.Code);
    }

    [Fact]
    public void Confirm_LaboratoryBriefingCoversWholePeriod_Confirms()
    {
        var schedule = Laboratory();
        var slot = Tomorrow(10, 12);
        var id = Submit(schedule, slot, ReservationPurpose.LaboratoryWork);
        var briefing = new BriefingValidity(OrganizerId, Now.AddMonths(-6), slot.End);

        schedule.Confirm(id, Manager, briefing, Now);

        Assert.Equal(ReservationStatus.Confirmed, schedule.Get(id).Status);
    }

    [Fact]
    public void Confirm_LaboratoryBriefingExpiresBeforeEnd_RejectsWithBriefingReason()
    {
        var schedule = Laboratory();
        var slot = Tomorrow(10, 12);
        var id = Submit(schedule, slot, ReservationPurpose.LaboratoryWork);
        var briefing = new BriefingValidity(OrganizerId, Now.AddMonths(-11), slot.End.AddMinutes(-1));

        schedule.Confirm(id, Manager, briefing, Now);

        Assert.Equal(ReservationStatus.Rejected, schedule.Get(id).Status);
        Assert.Equal(RejectionCode.BriefingNotValid, schedule.Get(id).Rejection!.Code);
    }

    [Fact]
    public void Confirm_ByOrganizer_RefusesWithoutChanges()
    {
        var schedule = MeetingRoom();
        var id = Submit(schedule, Tomorrow(10, 11));
        var before = StateOf(schedule);

        Assert.Throws<DomainException>(() => schedule.Confirm(id, Organizer, null, Now));

        Assert.Equal(before, StateOf(schedule));
    }
}
