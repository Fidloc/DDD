using Briefings.Application;
using Briefings.Domain;
using Briefings.Infrastructure;
using Xunit;

namespace Coworking.Tests.Briefings.Integration;

public sealed class BriefingStorageTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 10, 0, 0, TimeSpan.FromHours(2));
    private static readonly Actor Officer = new(PersonId.New(), BriefingRole.BriefingOfficer);

    [Fact]
    public async Task Save_ThenRestore_ReturnsDistinctInstanceWithoutNewEvents_AndAllowsSubsequentRevoke()
    {
        var person = PersonId.New();
        var original = new BriefingRecord(person, BriefingPolicy.Default);
        original.RecordBriefing(Officer, Now.AddDays(-20), Now);
        var session = new BriefingMemorySession(original);

        var loaded = await session.FindAsync(person);
        Assert.NotNull(loaded);
        var restored = loaded.Aggregate;

        Assert.NotSame(original, restored);
        Assert.Equal(original, restored);
        Assert.Equal(original.EffectiveValidity(), restored.EffectiveValidity());
        Assert.Empty(restored.Events);

        var service = new BriefingService(session, session, new StubActorContext(Officer), new StubClock(Now));
        await service.RevokeBriefingAsync(person, "Дострокове анулювання допуску");

        var afterRevoke = await session.FindAsync(person);
        Assert.NotNull(afterRevoke);
        Assert.Equal(2L, afterRevoke.Revision);
        Assert.Equal(Now, afterRevoke.Aggregate.RevokedAt);
        Assert.Empty(afterRevoke.Aggregate.Events);
    }

    [Fact]
    public async Task SaveChanges_WhenRevisionMismatches_RejectsConcurrentWrite()
    {
        var person = PersonId.New();
        var original = new BriefingRecord(person, BriefingPolicy.Default);
        original.RecordBriefing(Officer, Now.AddDays(-30), Now);
        var session = new BriefingMemorySession(original);

        var firstRead = (await session.FindAsync(person))!;
        var secondRead = (await session.FindAsync(person))!;

        firstRead.Aggregate.Revoke(Officer, "Перша зміна", Now);
        session.Stage(firstRead.Aggregate, firstRead.Revision);
        await session.SaveChangesAsync();

        secondRead.Aggregate.RecordBriefing(Officer, Now.AddDays(-5), Now);
        session.Stage(secondRead.Aggregate, secondRead.Revision);

        await Assert.ThrowsAsync<DomainException>(() => session.SaveChangesAsync());
    }

    private sealed record StubActorContext(Actor CurrentActor) : IActorContext;
    private sealed record StubClock(DateTimeOffset Now) : IClock;
}
