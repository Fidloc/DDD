using Xunit;
using ZoneUsage.Domain;

namespace Coworking.Tests.ZoneUsage.Domain;

/// <summary>Правила агрегату «Використання зони» (контекст «Фіксація використання зони»).</summary>
public sealed class ZoneUsageRecordTests
{
    private static readonly DateTimeOffset SlotStart = new(2026, 11, 17, 10, 0, 0, TimeSpan.FromHours(2));
    private static readonly TimeSlot Slot = new(SlotStart, SlotStart.AddHours(2));
    private static readonly Actor Organizer = new(UserId.New(), UsageRole.Organizer);
    private static readonly Actor Administrator = new(UserId.New(), UsageRole.SpaceAdministrator);

    private static ZoneUsageRecord Expected() =>
        ZoneUsageRecord.ExpectFrom(
            new ConfirmedSlot(ReservationId.New(), ZoneId.New(), Organizer.UserId, Slot),
            UsagePolicy.Default);

    private static string StateOf(ZoneUsageRecord u) =>
        $"{u.Status}|{u.StartedAt}|{u.CompletedAt}|{u.DelayWasReported}|" +
        string.Join(",", u.Events.Select(e => e.GetType().Name));

    [Theory]
    [InlineData(-1)]   // за хвилину до початку
    [InlineData(120)]  // рівно в момент закінчення періоду
    public void Start_OutsidePeriod_RefusesWithoutChanges(int minutesFromStart)
    {
        var usage = Expected();
        var before = StateOf(usage);

        Assert.Throws<DomainException>(() => usage.Start(Organizer, SlotStart.AddMinutes(minutesFromStart)));

        Assert.Equal(before, StateOf(usage));
    }

    [Fact]
    public void DeclareNoShow_ExactlyWhenGracePeriodEnds_Declares()
    {
        var usage = Expected();

        usage.DeclareNoShow(Administrator, SlotStart.AddMinutes(15));

        Assert.Equal(UsageStatus.NoShow, usage.Status);
        Assert.IsType<NoShowDeclared>(Assert.Single(usage.Events));
    }

    [Fact]
    public void DeclareNoShow_AfterDelayReported_RefusesWithoutChanges()
    {
        var usage = Expected();
        usage.ReportDelay(Organizer, SlotStart.AddMinutes(10));
        var before = StateOf(usage);

        Assert.Throws<DomainException>(() => usage.DeclareNoShow(Administrator, SlotStart.AddMinutes(30)));

        Assert.Equal(before, StateOf(usage));
    }

    [Fact]
    public void CancelExpectation_WhenExpected_PreventsLaterNoShow()
    {
        var usage = Expected();

        usage.CancelExpectation(SlotStart.AddHours(-3));

        Assert.Equal(UsageStatus.Cancelled, usage.Status);
        Assert.Throws<DomainException>(() => usage.DeclareNoShow(Administrator, SlotStart.AddMinutes(30)));
        Assert.IsType<UsageExpectationCancelled>(Assert.Single(usage.Events));
    }
}
