using Reservations.Domain;

namespace Coworking.Tests.Reservations.Domain;

internal static class ReservationFixture
{
    public static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 0, 0, TimeSpan.FromHours(2));
    public static readonly DateTimeOffset TomorrowMidnight = new(2026, 11, 17, 0, 0, 0, TimeSpan.FromHours(2));

    public static readonly UserId OrganizerId = UserId.New();
    public static readonly Actor Organizer = new(OrganizerId, ReservationRole.Organizer);
    public static readonly Actor Manager = new(UserId.New(), ReservationRole.ZoneManager);

    public static TimeSlot Tomorrow(double from, double to) =>
        new(TomorrowMidnight.AddHours(from), TomorrowMidnight.AddHours(to));

    public static ZoneSchedule MeetingRoom() =>
        new(ZoneId.New(), new ZoneProfile(ZoneCategory.MeetingRoom, 10), BookingPolicy.Default);

    public static ZoneSchedule Laboratory() =>
        new(ZoneId.New(), new ZoneProfile(ZoneCategory.Laboratory, 6), BookingPolicy.Default);

    public static ReservationId Submit(ZoneSchedule schedule, TimeSlot slot,
        ReservationPurpose purpose = ReservationPurpose.Meeting)
    {
        var id = ReservationId.New();
        schedule.Submit(id, Organizer, slot, purpose, 5, Now);
        return id;
    }

    public static ReservationId Confirmed(ZoneSchedule schedule, TimeSlot slot)
    {
        var id = Submit(schedule, slot);
        schedule.Confirm(id, Manager, null, Now);
        return id;
    }

    public static string StateOf(ZoneSchedule schedule) =>
        string.Join(";", schedule.Reservations.Select(r => $"{r.Id.Value}:{r.Status}:{r.Rejection?.Code}"))
        + "|" + string.Join(",", schedule.Events.Select(e => e.GetType().Name));
}
