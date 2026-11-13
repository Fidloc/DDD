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
        Assert.IsType<ReservationRejected>(schedule.Events[^1]);
    }

    [Fact]
    public void Confirm_AdjacentToConfirmedReservation_Confirms()
    {
        var schedule = MeetingRoom();
        Confirmed(schedule, Tomorrow(10, 12));
        var next = Submit(schedule, Tomorrow(12, 13));

        schedule.Confirm(next, Manager, null, Now);

        Assert.Equal(ReservationStatus.Confirmed, schedule.Get(next).Status);
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
    public void Submit_DurationEqualsMaximum_IsAccepted()
    {
        var schedule = MeetingRoom();

        var id = Submit(schedule, Tomorrow(9, 17));

        Assert.Equal(ReservationStatus.Requested, schedule.Get(id).Status);
    }

    [Theory]
    [InlineData(-60, 60)]          // період уже розпочався
    [InlineData(60, 20)]           // коротше за 30 хвилин
    [InlineData(60, 481)]          // довше за 8 годин на 1 хвилину
    [InlineData(31 * 24 * 60, 60)] // за горизонтом 30 днів
    public void Submit_OutsidePolicy_RefusesWithoutChanges(int startInMinutes, int durationMinutes)
    {
        var schedule = MeetingRoom();
        var start = Now.AddMinutes(startInMinutes);
        var slot = new TimeSlot(start, start.AddMinutes(durationMinutes));

        Assert.Throws<DomainException>(() =>
            schedule.Submit(ReservationId.New(), Organizer, slot, ReservationPurpose.Meeting, 5, Now));

        Assert.Empty(schedule.Reservations);
        Assert.Empty(schedule.Events);
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

    [Fact]
    public void Cancel_AfterUsageStarted_RefusesWithoutChanges()
    {
        var schedule = MeetingRoom();
        var slot = Tomorrow(10, 12);
        var id = Confirmed(schedule, slot);
        schedule.RecordUsageStarted(id, slot.Start.AddMinutes(5));
        var before = StateOf(schedule);

        Assert.Throws<DomainException>(() => schedule.Cancel(id, Organizer, Now));

        Assert.Equal(before, StateOf(schedule));
    }
}
