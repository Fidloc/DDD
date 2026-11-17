using Reservations.Domain;

namespace Reservations.Application;

public sealed record LoadedZoneSchedule(ZoneSchedule Aggregate, long Revision);

public interface IZoneScheduleRepository
{
    Task<LoadedZoneSchedule?> FindAsync(ZoneId zoneId);
    void Stage(ZoneSchedule schedule, long expectedRevision);
}
