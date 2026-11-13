using Reservations.Domain;
using Xunit;
using static Coworking.Tests.Reservations.Domain.ReservationFixture;

namespace Coworking.Tests.Reservations.Domain;

public sealed class ValueObjectTests
{
    [Fact]
    public void TimeSlot_EndNotAfterStart_Rejects()
    {
        var start = TomorrowMidnight.AddHours(10);

        Assert.Throws<DomainException>(() => new TimeSlot(start, start));
        Assert.Throws<DomainException>(() => new TimeSlot(start, start.AddMinutes(-30)));
    }

    [Fact]
    public void TimeSlot_SameBounds_AreEqualValues()
    {
        var a = Tomorrow(10, 11);
        var b = Tomorrow(10, 11);

        Assert.Equal(a, b);
        Assert.NotSame(a, b);
    }

    [Fact]
    public void Reservation_SameFieldsDifferentIds_AreDifferentEntities()
    {
        var schedule = MeetingRoom();
        var first = schedule.Get(Submit(schedule, Tomorrow(10, 11)));
        var second = schedule.Get(Submit(schedule, Tomorrow(10, 11)));

        Assert.Equal(first.Slot, second.Slot);
        Assert.NotEqual(first, second);
    }
}
