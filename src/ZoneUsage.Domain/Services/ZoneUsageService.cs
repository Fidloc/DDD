using ZoneUsage.Domain;

namespace ZoneUsage.Application;

public sealed class ZoneUsageService
{
    private readonly IZoneUsageRepository repository;
    private readonly IUnitOfWork unitOfWork;
    private readonly IActorContext actorContext;
    private readonly IClock clock;

    public ZoneUsageService(
        IZoneUsageRepository repository,
        IUnitOfWork unitOfWork,
        IActorContext actorContext,
        IClock clock)
    {
        this.repository = repository;
        this.unitOfWork = unitOfWork;
        this.actorContext = actorContext;
        this.clock = clock;
    }

    public async Task StartUsageAsync(ReservationId reservationId)
    {
        var loaded = await repository.FindAsync(reservationId)
            ?? throw new DomainException("Запис очікуваного використання зони не знайдено.");

        loaded.Aggregate.Start(actorContext.CurrentActor, clock.Now);

        repository.Stage(loaded.Aggregate, loaded.Revision);
        await unitOfWork.SaveChangesAsync();
    }

    public async Task CompleteUsageAsync(ReservationId reservationId)
    {
        var loaded = await repository.FindAsync(reservationId)
            ?? throw new DomainException("Запис використання зони не знайдено.");

        loaded.Aggregate.Complete(actorContext.CurrentActor, clock.Now);

        repository.Stage(loaded.Aggregate, loaded.Revision);
        await unitOfWork.SaveChangesAsync();
    }

    public async Task DeclareNoShowAsync(ReservationId reservationId)
    {
        var loaded = await repository.FindAsync(reservationId)
            ?? throw new DomainException("Запис використання зони не знайдено.");

        loaded.Aggregate.DeclareNoShow(actorContext.CurrentActor, clock.Now);

        repository.Stage(loaded.Aggregate, loaded.Revision);
        await unitOfWork.SaveChangesAsync();
    }
}
