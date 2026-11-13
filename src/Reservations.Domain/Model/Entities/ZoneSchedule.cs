namespace Reservations.Domain;

public sealed class ZoneSchedule
{
    private readonly List<Reservation> reservations = [];
    private readonly List<DomainEvent> events = [];

    public ZoneId ZoneId { get; }
    public ZoneProfile Zone { get; }
    public BookingPolicy Policy { get; }

    public IReadOnlyList<Reservation> Reservations => reservations.AsReadOnly();
    public IReadOnlyList<DomainEvent> Events => events.AsReadOnly();

    public ZoneSchedule(ZoneId zoneId, ZoneProfile zone, BookingPolicy policy)
    {
        ZoneId = zoneId;
        Zone = zone;
        Policy = policy;
    }

    public Reservation Get(ReservationId id) =>
        reservations.FirstOrDefault(r => r.Id == id)
        ?? throw new DomainException("Резервування не знайдено в розкладі цієї зони.");
    
    public Reservation Submit(ReservationId id, Actor actor, TimeSlot slot,
        ReservationPurpose purpose, int participants, DateTimeOffset now)
    {
        RequireRole(actor, ReservationRole.Organizer);
        if (reservations.Any(r => r.Id == id))
            throw new DomainException("Заявку з таким ідентифікатором уже подано.");
        Policy.EnsureAllows(slot, now);
        var reservation = new Reservation(id, actor.UserId, slot, purpose, participants);

        reservations.Add(reservation);
        events.Add(new ReservationRequested(id, ZoneId, actor.UserId, slot, now));
        return reservation;
    }

    public void Confirm(ReservationId id, Actor actor, BriefingValidity? briefing, DateTimeOffset now)
    {
        RequireRole(actor, ReservationRole.ZoneManager);
        var reservation = Get(id);
        RequireStatus(reservation, ReservationStatus.Requested);
        if (reservation.Slot.Start <= now)
            throw new DomainException("Період уже розпочався: заявку більше не можна погодити.");

        var rejection = FindRejection(reservation, briefing);
        if (rejection is null)
        {
            reservation.Confirm();
            events.Add(new ReservationConfirmed(id, ZoneId, reservation.Organizer, reservation.Slot, now));
        }
        else
        {
            reservation.Reject(rejection);
            events.Add(new ReservationRejected(id, rejection, now));
        }
    }

    public void Reject(ReservationId id, Actor actor, string reason, DateTimeOffset now)
    {
        RequireRole(actor, ReservationRole.ZoneManager);
        var reservation = Get(id);
        RequireStatus(reservation, ReservationStatus.Requested);
        var rejection = new Rejection(RejectionCode.ManagerDecision, reason);

        reservation.Reject(rejection);
        events.Add(new ReservationRejected(id, rejection, now));
    }

    public void Cancel(ReservationId id, Actor actor, DateTimeOffset now)
    {
        RequireRole(actor, ReservationRole.Organizer);
        var reservation = Get(id);
        if (reservation.Organizer != actor.UserId)
            throw new DomainException("Скасувати резервування може лише його організатор.");
        if (reservation.Status is ReservationStatus.InUse or ReservationStatus.Completed)
            throw new DomainException("Скасування неможливе після початку використання.");
        if (reservation.Status is not (ReservationStatus.Requested or ReservationStatus.Confirmed))
            throw new DomainException($"Резервування у стані {reservation.Status} не можна скасувати.");
        if (now >= reservation.Slot.Start)
            throw new DomainException("Період уже розпочався: скасування неможливе.");

        var wasConfirmed = reservation.Status == ReservationStatus.Confirmed;
        reservation.Cancel();
        events.Add(new ReservationCancelled(id, wasConfirmed, now));
    }

    public void RecordUsageStarted(ReservationId id, DateTimeOffset at)
    {
        var reservation = Get(id);
        RequireStatus(reservation, ReservationStatus.Confirmed);
        if (!reservation.Slot.Contains(at))
            throw new DomainException("Початок використання поза межами підтвердженого періоду.");

        reservation.TakeIntoUse();
        events.Add(new ReservationTakenIntoUse(id, at));
    }

    public void RecordUsageCompleted(ReservationId id, DateTimeOffset at)
    {
        var reservation = Get(id);
        RequireStatus(reservation, ReservationStatus.InUse);

        reservation.Complete();
        events.Add(new ZoneReleased(id, ReleaseReason.UsageCompleted, at));
    }

    public void RecordNoShow(ReservationId id, DateTimeOffset at)
    {
        var reservation = Get(id);
        RequireStatus(reservation, ReservationStatus.Confirmed);

        reservation.MarkNoShow();
        events.Add(new ZoneReleased(id, ReleaseReason.NoShow, at));
    }

    private Rejection? FindRejection(Reservation candidate, BriefingValidity? briefing)
    {
        var conflict = reservations.FirstOrDefault(r =>
            !r.Equals(candidate) && r.BlocksZone && r.Slot.Overlaps(candidate.Slot));
        if (conflict is not null)
            return new Rejection(RejectionCode.Conflict,
                $"Зона вже зайнята підтвердженим резервуванням {conflict.Slot.Start:g}–{conflict.Slot.End:t}.");

        if (!Zone.Allows(candidate.Purpose))
            return new Rejection(RejectionCode.PurposeMismatch,
                $"Зона категорії {Zone.Category} не призначена для мети {candidate.Purpose}.");

        if (candidate.Participants > Zone.Capacity)
            return new Rejection(RejectionCode.CapacityExceeded,
                $"Учасників {candidate.Participants}, а місткість зони {Zone.Capacity}.");

        if (Zone.RequiresBriefing &&
            (briefing is null || !briefing.CoversWholeSlot(candidate.Organizer, candidate.Slot)))
            return new Rejection(RejectionCode.BriefingNotValid,
                "Інструктаж організатора не чинний на весь запитаний період.");

        return null;
    }

    private static void RequireRole(Actor actor, ReservationRole role)
    {
        if (actor.Role != role)
            throw new DomainException($"Команду може виконати лише роль {role}.");
    }

    private static void RequireStatus(Reservation reservation, ReservationStatus expected)
    {
        if (reservation.Status != expected)
            throw new DomainException(
                $"Очікувався стан {expected}, а резервування перебуває у стані {reservation.Status}.");
    }
}
