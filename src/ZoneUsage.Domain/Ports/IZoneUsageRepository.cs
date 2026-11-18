using ZoneUsage.Domain;

namespace ZoneUsage.Application;

public sealed record LoadedZoneUsage(ZoneUsageRecord Aggregate, long Revision);

public interface IZoneUsageRepository
{
    Task<LoadedZoneUsage?> FindAsync(ReservationId reservationId);
    void Stage(ZoneUsageRecord usage, long expectedRevision);
}
