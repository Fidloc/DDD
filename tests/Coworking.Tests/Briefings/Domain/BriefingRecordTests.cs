using Briefings.Domain;
using Xunit;

namespace Coworking.Tests.Briefings.Domain;

public sealed class BriefingRecordTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 0, 0, TimeSpan.FromHours(2));
    private static readonly Actor Officer = new(PersonId.New(), BriefingRole.BriefingOfficer);

    private static BriefingRecord NewRecord() => new(PersonId.New(), BriefingPolicy.Default);

    private static BriefingRecord WithBriefing(DateTimeOffset conductedAt)
    {
        var record = NewRecord();
        record.RecordBriefing(Officer, conductedAt, Now);
        return record;
    }

    private static string StateOf(BriefingRecord r) =>
        $"{r.Passes.Count}|{r.RevokedAt}|{r.EffectiveValidity()?.Until}|" +
        string.Join(",", r.Events.Select(e => e.GetType().Name));

    [Fact]
    public void RecordBriefing_ByOfficer_SetsValidityForPolicyPeriod()
    {
        var conducted = Now.AddDays(-10);

        var record = WithBriefing(conducted);

        var validity = record.EffectiveValidity();
        Assert.NotNull(validity);
        Assert.Equal(conducted, validity.From);
        Assert.Equal(conducted.AddDays(365), validity.Until);
        Assert.IsType<BriefingRecorded>(Assert.Single(record.Events));
    }

    [Fact]
    public void RecordBriefing_InFuture_RefusesWithoutChanges()
    {
        var record = NewRecord();
        var before = StateOf(record);

        Assert.Throws<DomainException>(() => record.RecordBriefing(Officer, Now.AddDays(1), Now));

        Assert.Equal(before, StateOf(record));
    }

    [Fact]
    public void IsValidThroughout_PeriodEndsExactlyAtExpiry_IsTrue()
    {
        var record = WithBriefing(Now.AddDays(-364));
        var expiry = record.EffectiveValidity()!.Until;

        Assert.True(record.IsValidThroughout(expiry.AddHours(-2), expiry));
    }

    [Fact]
    public void Revoke_ValidBriefing_CutsValidityAtRevocationMoment()
    {
        var record = WithBriefing(Now.AddDays(-30));

        record.Revoke(Officer, "Порушення правил безпеки", Now);

        Assert.Equal(Now, record.EffectiveValidity()!.Until);
        Assert.False(record.IsValidThroughout(Now.AddHours(1), Now.AddHours(2)));
        Assert.IsType<BriefingRevoked>(record.Events[^1]);
    }
}
