using Briefings.Domain;

namespace Briefings.Application;


public sealed class BriefingService
{
    private readonly IBriefingRecordRepository repository;
    private readonly IUnitOfWork unitOfWork;
    private readonly IActorContext actorContext;
    private readonly IClock clock;

    public BriefingService(
        IBriefingRecordRepository repository,
        IUnitOfWork unitOfWork,
        IActorContext actorContext,
        IClock clock)
    {
        this.repository = repository;
        this.unitOfWork = unitOfWork;
        this.actorContext = actorContext;
        this.clock = clock;
    }

    public async Task RecordBriefingAsync(PersonId person, DateTimeOffset conductedAt)
    {
        var loaded = await repository.FindAsync(person);
        var record = loaded?.Aggregate ?? new BriefingRecord(person, BriefingPolicy.Default);
        var expectedRevision = loaded?.Revision ?? 0L;

        record.RecordBriefing(actorContext.CurrentActor, conductedAt, clock.Now);

        repository.Stage(record, expectedRevision);
        await unitOfWork.SaveChangesAsync();
    }

    public async Task RevokeBriefingAsync(PersonId person, string reason)
    {
        var loaded = await repository.FindAsync(person)
            ?? throw new DomainException("Облікову картку інструктажів для вказаної особи не знайдено.");

        loaded.Aggregate.Revoke(actorContext.CurrentActor, reason, clock.Now);

        repository.Stage(loaded.Aggregate, loaded.Revision);
        await unitOfWork.SaveChangesAsync();
    }
}
