using Briefings.Domain;

namespace Briefings.Application;

public sealed record LoadedBriefingRecord(BriefingRecord Aggregate, long Revision);

public interface IBriefingRecordRepository
{
    Task<LoadedBriefingRecord?> FindAsync(PersonId person);
    void Stage(BriefingRecord record, long expectedRevision);
}
