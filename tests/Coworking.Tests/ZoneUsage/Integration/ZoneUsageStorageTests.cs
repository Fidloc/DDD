using Xunit;
using ZoneUsage.Application;
using ZoneUsage.Domain;
using ZoneUsage.Infrastructure;

namespace Coworking.Tests.ZoneUsage.Integration;

public sealed class ZoneUsageStorageTests
{
    private static readonly DateTimeOffset SlotStart = new(2026, 11, 17, 10, 0, 0, TimeSpan.FromHours(2));
    private static readonly TimeSlot Slot = new(SlotStart, SlotStart.AddHours(2));
    private static readonly Actor Organizer = new(UserId.New(), UsageRole.Organizer);
    private static readonly Actor Administrator = new(UserId.New(), UsageRole.SpaceAdministrator);

    private static ZoneUsageRecord CreateExpected() =>
        ZoneUsageRecord.ExpectFrom(
            new ConfirmedSlot(ReservationId.New(), ZoneId.New(), Organizer.UserId, Slot),
            UsagePolicy.Default);

    [Fact]
    public async Task Save_ThenRestore_ReturnsDistinctInstanceWithoutNewEvents_AndAllowsCompletion()
    {
        var original = CreateExpected();
        var session = new ZoneUsageMemorySession(original);
        var startService = new ZoneUsageService(
            session, session,
            new StubActorContext(Organizer),
            new StubClock(SlotStart.AddMinutes(5)));

        await startService.StartUsageAsync(original.ReservationId);

        var loaded = await session.FindAsync(original.ReservationId);
        Assert.NotNull(loaded);
        var restored = loaded.Aggregate;

        Assert.NotSame(original, restored);
        Assert.Equal(original, restored);
        Assert.Equal(UsageStatus.InProgress, restored.Status);
        Assert.Empty(restored.Events);

        var completeService = new ZoneUsageService(
            session, session,
            new StubActorContext(Organizer),
            new StubClock(SlotStart.AddMinutes(90)));
        await completeService.CompleteUsageAsync(original.ReservationId);

        var afterComplete = await session.FindAsync(original.ReservationId);
        Assert.NotNull(afterComplete);
        Assert.Equal(3L, afterComplete.Revision);
        Assert.Equal(UsageStatus.Completed, afterComplete.Aggregate.Status);
        Assert.Empty(afterComplete.Aggregate.Events);
    }

    [Fact]
    public async Task SaveChanges_WhenRevisionMismatches_RejectsConcurrentWrite()
    {
        var original = CreateExpected();
        var session = new ZoneUsageMemorySession(original);

        var firstRead = (await session.FindAsync(original.ReservationId))!;
        var secondRead = (await session.FindAsync(original.ReservationId))!;

        firstRead.Aggregate.Start(Organizer, SlotStart.AddMinutes(3));
        session.Stage(firstRead.Aggregate, firstRead.Revision);
        await session.SaveChangesAsync();

        secondRead.Aggregate.DeclareNoShow(Administrator, SlotStart.AddMinutes(20));
        session.Stage(secondRead.Aggregate, secondRead.Revision);

        await Assert.ThrowsAsync<DomainException>(() => session.SaveChangesAsync());
    }

    private sealed record StubActorContext(Actor CurrentActor) : IActorContext;
    private sealed record StubClock(DateTimeOffset Now) : IClock;
}
