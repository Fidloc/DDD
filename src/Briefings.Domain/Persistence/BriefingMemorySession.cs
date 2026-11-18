using Briefings.Application;
using Briefings.Domain;

namespace Briefings.Infrastructure;

/// <summary>
/// Навчальний адаптер сховища та одиниці роботи в оперативній пам'яті для контексту «Облік інструктажів».
/// Обмеження: дані існують лише в межах поточного процесу та не зберігаються між запусками застосунку.
/// </summary>
public sealed class BriefingMemorySession : IBriefingRecordRepository, IUnitOfWork
{
    private readonly Dictionary<PersonId, (BriefingRecordState State, long Revision)> rows = [];
    private readonly List<(BriefingRecordState State, long ExpectedRevision)> staged = [];

    public BriefingMemorySession(params BriefingRecord[] initialRecords)
    {
        foreach (var record in initialRecords)
        {
            rows[record.Person] = (record.ToState(), 1L);
        }
    }

    public Task<LoadedBriefingRecord?> FindAsync(PersonId person)
    {
        if (!rows.TryGetValue(person, out var entry))
            return Task.FromResult<LoadedBriefingRecord?>(null);

        var restored = BriefingRecord.Restore(entry.State);
        return Task.FromResult<LoadedBriefingRecord?>(new LoadedBriefingRecord(restored, entry.Revision));
    }

    public void Stage(BriefingRecord record, long expectedRevision)
    {
        ArgumentNullException.ThrowIfNull(record);
        staged.Add((record.ToState(), expectedRevision));
    }

    public Task SaveChangesAsync()
    {
        foreach (var item in staged)
        {
            var currentRevision = rows.TryGetValue(item.State.Person, out var existing)
                ? existing.Revision
                : 0L;

            if (currentRevision != item.ExpectedRevision)
            {
                staged.Clear();
                throw new DomainException(
                    $"Конфлікт ревізій картки інструктажів: очікувалася {item.ExpectedRevision}, але в сховищі {currentRevision}.");
            }
        }

        foreach (var item in staged)
        {
            rows[item.State.Person] = (item.State, item.ExpectedRevision + 1L);
        }

        staged.Clear();
        return Task.CompletedTask;
    }
}
