using ZoneUsage.Application;
using ZoneUsage.Domain;

namespace ZoneUsage.Infrastructure;


public sealed class ZoneUsageMemorySession : IZoneUsageRepository, IUnitOfWork
{
    private readonly Dictionary<ReservationId, (ZoneUsageState State, long Revision)> rows = [];
    private readonly List<(ZoneUsageState State, long ExpectedRevision)> staged = [];

    public ZoneUsageMemorySession(params ZoneUsageRecord[] initialRecords)
    {
        foreach (var record in initialRecords)
        {
            rows[record.ReservationId] = (record.ToState(), 1L);
        }
    }

    public Task<LoadedZoneUsage?> FindAsync(ReservationId reservationId)
    {
        if (!rows.TryGetValue(reservationId, out var entry))
            return Task.FromResult<LoadedZoneUsage?>(null);

        var restored = ZoneUsageRecord.Restore(entry.State);
        return Task.FromResult<LoadedZoneUsage?>(new LoadedZoneUsage(restored, entry.Revision));
    }

    public void Stage(ZoneUsageRecord usage, long expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(usage);
        staged.Add((usage.ToState(), expectedRevision));
    }

    public Task SaveChangesAsync()
    {
        foreach (var item in staged)
        {
            var currentRevision = rows.TryGetValue(item.State.ReservationId, out var existing)
                ? existing.Revision
                : 0L;

            if (currentRevision != item.ExpectedRevision)
            {
                staged.Clear();
                throw new DomainException(
                    $"Конфлікт ревізій використання зони: очікувалася {item.ExpectedRevision}, але в сховищі {currentRevision}.");
            }
        }

        foreach (var item in staged)
        {
            rows[item.State.ReservationId] = (item.State, item.ExpectedRevision + 1L);
        }

        staged.Clear();
        return Task.CompletedTask;
    }
}
