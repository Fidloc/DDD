using Reservations.Application;
using Reservations.Domain;

namespace Reservations.Infrastructure;

/// <summary>
/// Навчальний адаптер сховища та одиниці роботи в оперативній пам'яті для контексту «Погодження резервування».
/// Обмеження: дані зберігаються лише в оперативній пам'яті процесу без міжпроцесної довговічності.
/// </summary>
public sealed class ReservationMemorySession : IZoneScheduleRepository, IUnitOfWork
{
    private readonly Dictionary<ZoneId, (ZoneScheduleState State, long Revision)> rows = [];
    private readonly List<(ZoneScheduleState State, long ExpectedRevision)> staged = [];

    public ReservationMemorySession(params ZoneSchedule[] initialSchedules)
    {
        foreach (var schedule in initialSchedules)
        {
            rows[schedule.ZoneId] = (schedule.ToState(), 1L);
        }
    }

    public Task<LoadedZoneSchedule?> FindAsync(ZoneId zoneId)
    {
        if (!rows.TryGetValue(zoneId, out var entry))
            return Task.FromResult<LoadedZoneSchedule?>(null);

        var restored = ZoneSchedule.Restore(entry.State);
        return Task.FromResult<LoadedZoneSchedule?>(new LoadedZoneSchedule(restored, entry.Revision));
    }

    public void Stage(ZoneSchedule schedule, long expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        staged.Add((schedule.ToState(), expectedRevision));
    }

    public Task SaveChangesAsync()
    {
        foreach (var item in staged)
        {
            var currentRevision = rows.TryGetValue(item.State.ZoneId, out var existing)
                ? existing.Revision
                : 0L;

            if (currentRevision != item.ExpectedRevision)
            {
                staged.Clear();
                throw new DomainException(
                    $"Конфлікт ревізій розкладу зони: очікувалася {item.ExpectedRevision}, але в сховищі {currentRevision}.");
            }
        }

        foreach (var item in staged)
        {
            rows[item.State.ZoneId] = (item.State, item.ExpectedRevision + 1L);
        }

        staged.Clear();
        return Task.CompletedTask;
    }
}
