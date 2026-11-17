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
    public async Task RecordBriefing_WhenUnauthorizedOrganizer_RefusesWithoutStagingOrSaving()
    {
        var person = PersonId.New();
        var spyRepo = new SpyBriefingRepository();
        var service = new BriefingService(spyRepo, spyRepo, new StubActorContext(Organizer), new StubClock(Now));

        await Assert.ThrowsAsync<DomainException>(() =>
            service.RecordBriefingAsync(person, Now.AddHours(-2)));

        Assert.Equal(0, spyRepo.StagedCount);
        Assert.Equal(0, spyRepo.SavedCount);
    }

    [Fact]
    public async Task RevokeBriefing_WhenUnitOfWorkFails_ThrowsAndLeavesStoredStateUnchanged()
    {
        var person = PersonId.New();
        var existing = new BriefingRecord(person, BriefingPolicy.Default);
        existing.RecordBriefing(Officer, Now.AddDays(-10), Now);
        var session = new BriefingMemorySession(existing);
        var failingUow = new FailingUnitOfWork();
        var service = new BriefingService(session, failingUow, new StubActorContext(Officer), new StubClock(Now));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RevokeBriefingAsync(person, "Порушення регламенту"));

        var reloaded = await session.FindAsync(person);
        Assert.NotNull(reloaded);
        Assert.Null(reloaded.Aggregate.RevokedAt);
        Assert.Equal(1L, reloaded.Revision);
    }

    private sealed record StubActorContext(Actor CurrentActor) : IActorContext;
    private sealed record StubClock(DateTimeOffset Now) : IClock;

    private sealed class FailingUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync() =>
            throw new InvalidOperationException("Технічний збій фіксації транзакції.");
    }

    private sealed class SpyBriefingRepository : IBriefingRecordRepository, IUnitOfWork
    {
        public int StagedCount { get; private set; }
        public int SavedCount { get; private set; }

        public Task<LoadedBriefingRecord?> FindAsync(PersonId person) =>
            Task.FromResult<LoadedBriefingRecord?>(null);

        public void Stage(BriefingRecord record, long expectedRevision) => StagedCount++;

        public Task SaveChangesAsync()
        {
            SavedCount++;
            return Task.CompletedTask;
        }
    }
}
