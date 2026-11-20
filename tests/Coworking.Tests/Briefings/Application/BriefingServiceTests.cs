using Briefings.Application;
using Briefings.Domain;
using Briefings.Infrastructure;
using Xunit;

namespace Coworking.Tests.Briefings.Application;

public sealed class BriefingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.FromHours(2));
    private static readonly Actor Officer = new(PersonId.New(), BriefingRole.BriefingOfficer);
    private static readonly Actor Organizer = new(PersonId.New(), BriefingRole.Organizer);

    [Fact]
    public async Task RecordBriefing_WhenAuthorizedOfficer_StagesAndSavesChanges()
    {
        var person = PersonId.New();
        var session = new BriefingMemorySession();
        var service = new BriefingService(session, session, new StubActorContext(Officer), new StubClock(Now));

        await service.RecordBriefingAsync(person, Now.AddDays(-1));

        var loaded = await session.FindAsync(person);
        Assert.NotNull(loaded);
        Assert.Equal(1L, loaded.Revision);
        Assert.Single(loaded.Aggregate.Passes);
        Assert.Equal(Now.AddDays(-1).AddDays(365), loaded.Aggregate.EffectiveValidity()!.Until);
    }

    [Fact]
    public async Task RecordBriefing_WhenUnauthorizedOrganizer_RefusesWithoutUpdatingSession()
    {
        var person = PersonId.New();
        var session = new BriefingMemorySession();
        var service = new BriefingService(session, session, new StubActorContext(Organizer), new StubClock(Now));

        await Assert.ThrowsAsync<DomainException>(() =>
            service.RecordBriefingAsync(person, Now.AddHours(-2)));

        Assert.Null(await session.FindAsync(person));
    }

    private sealed record StubActorContext(Actor CurrentActor) : IActorContext;
    private sealed record StubClock(DateTimeOffset Now) : IClock;
}
