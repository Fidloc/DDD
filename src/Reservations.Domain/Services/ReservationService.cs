using Reservations.Domain;

namespace Reservations.Application;

/// <summary>
/// Прикладна служба контексту «Погодження резервування».
/// Отримує довірені зовнішні факти (актор, час, чинність інструктажу),
/// завантажує розклад зони, делегує рішення агрегату та фіксує результат.
/// </summary>
public sealed class ReservationService
{
    private readonly IZoneScheduleRepository repository;
    private readonly IUnitOfWork unitOfWork;
    private readonly IActorContext actorContext;
    private readonly IClock clock;
    private readonly IBriefingFacts briefingFacts;

    public ReservationService(
        IZoneScheduleRepository repository,
        IUnitOfWork unitOfWork,
        IActorContext actorContext,
        IClock clock,
        IBriefingFacts briefingFacts)
    {
        this.repository = repository;
        this.unitOfWork = unitOfWork;
        this.actorContext = actorContext;
        this.clock = clock;
        this.briefingFacts = briefingFacts;
    }

    public async Task ConfirmReservationAsync(ZoneId zoneId, ReservationId reservationId)
    {
        var loaded = await repository.FindAsync(zoneId)
            ?? throw new DomainException("Розклад вказаної зони не знайдено у сховищі.");

        var schedule = loaded.Aggregate;
        var reservation = schedule.Get(reservationId);
        var briefing = schedule.Zone.RequiresBriefing
            ? await briefingFacts.GetValidityAsync(reservation.Organizer)
            : null;

        schedule.Confirm(reservationId, actorContext.CurrentActor, briefing, clock.Now);

        repository.Stage(schedule, loaded.Revision);
        await unitOfWork.SaveChangesAsync();
    }
}
