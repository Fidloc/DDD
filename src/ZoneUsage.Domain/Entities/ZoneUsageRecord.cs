namespace ZoneUsage.Domain;

public enum UsageStatus
{
    Expected,    
    InProgress,  
    Completed,   
    NoShow,      
    Cancelled    
}

public sealed record ZoneUsageState(
    ReservationId ReservationId,
    ZoneId ZoneId,
    UserId Organizer,
    TimeSlot Slot,
    UsagePolicy Policy,
    UsageStatus Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    bool DelayWasReported);

public sealed class ZoneUsageRecord : IEquatable<ZoneUsageRecord>
{
    private readonly List<DomainEvent> events = [];

    public ReservationId ReservationId { get; }
    public ZoneId ZoneId { get; }
    public UserId Organizer { get; }
    public TimeSlot Slot { get; }
    public UsagePolicy Policy { get; }
    public UsageStatus Status { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public bool DelayWasReported { get; private set; }

    public IReadOnlyList<DomainEvent> Events => events.AsReadOnly();

    public bool IsOccupied => Status == UsageStatus.InProgress;

    private ZoneUsageRecord(ConfirmedSlot fact, UsagePolicy policy)
    {
        ReservationId = fact.ReservationId;
        ZoneId = fact.ZoneId;
        Organizer = fact.Organizer;
        Slot = fact.Slot;
        Policy = policy;
        Status = UsageStatus.Expected;
    }

    private ZoneUsageRecord(ZoneUsageState state)
    {
        ReservationId = state.ReservationId;
        ZoneId = state.ZoneId;
        Organizer = state.Organizer;
        Slot = state.Slot;
        Policy = state.Policy;
        Status = state.Status;
        StartedAt = state.StartedAt;
        CompletedAt = state.CompletedAt;
        DelayWasReported = state.DelayWasReported;
    }

    public static ZoneUsageRecord ExpectFrom(ConfirmedSlot fact, UsagePolicy policy) => new(fact, policy);

    public static ZoneUsageRecord Restore(ZoneUsageState state)
    {
        if (state is null)
            throw new DomainException("Стан запису використання зони не може бути порожнім.");
        if (state.ReservationId is null || state.ZoneId is null || state.Organizer is null ||
            state.Slot is null || state.Policy is null)
            throw new DomainException("Усі ідентифікатори, інтервал та політика є обов'язковими для відновлення.");

        switch (state.Status)
        {
            case UsageStatus.Expected:
            case UsageStatus.Cancelled:
                if (state.StartedAt is not null || state.CompletedAt is not null)
                    throw new DomainException($"У стані {state.Status} не може бути зафіксованого часу початку чи завершення.");
                break;

            case UsageStatus.NoShow:
                if (state.StartedAt is not null || state.CompletedAt is not null)
                    throw new DomainException("У стані NoShow не може бути зафіксованого часу використання.");
                if (state.DelayWasReported)
                    throw new DomainException("Неузгоджений стан: неявка не може бути зафіксована за наявності повідомлення про затримку.");
                break;

            case UsageStatus.InProgress:
                if (state.StartedAt is not { } started || !state.Slot.Contains(started))
                    throw new DomainException("У стані InProgress має бути зафіксований початок у межах слоту.");
                if (state.CompletedAt is not null)
                    throw new DomainException("У стані InProgress не може бути зафіксованого часу завершення.");
                break;

            case UsageStatus.Completed:
                if (state.StartedAt is not { } startedAt || !state.Slot.Contains(startedAt))
                    throw new DomainException("У стані Completed має бути коректний час початку.");
                if (state.CompletedAt is not { } completedAt || completedAt < startedAt)
                    throw new DomainException("У стані Completed час завершення має бути не раніше за час початку.");
                break;

            default:
                throw new DomainException($"Невідомий статус використання зони: {state.Status}.");
        }

        return new ZoneUsageRecord(state);
    }

    public ZoneUsageState ToState() =>
        new(ReservationId, ZoneId, Organizer, Slot, Policy, Status, StartedAt, CompletedAt, DelayWasReported);

    public void Start(Actor actor, DateTimeOffset at)
    {
        RequireOrganizer(actor);
        RequireStatus(UsageStatus.Expected);
        if (!Slot.Contains(at))
            throw new DomainException("Початок поза межами підтвердженого періоду.");

        Status = UsageStatus.InProgress;
        StartedAt = at;
        events.Add(new UsageStarted(ReservationId, at));
    }

    public void Complete(Actor actor, DateTimeOffset at)
    {
        RequireOrganizer(actor);
        RequireStatus(UsageStatus.InProgress);
        if (at < StartedAt)
            throw new DomainException("Завершення не може передувати початку використання.");

        Status = UsageStatus.Completed;
        CompletedAt = at;
        events.Add(new UsageCompleted(ReservationId, at < Slot.End, at));
    }

    public void ReportDelay(Actor actor, DateTimeOffset at)
    {
        RequireOrganizer(actor);
        RequireStatus(UsageStatus.Expected);
        if (at >= Slot.End)
            throw new DomainException("Період уже завершився: повідомлення про затримку не приймається.");
        if (DelayWasReported)
            throw new DomainException("Про затримку вже повідомлено.");

        DelayWasReported = true;
        events.Add(new DelayReported(ReservationId, at));
    }

    public void DeclareNoShow(Actor actor, DateTimeOffset now)
    {
        if (actor.Role != UsageRole.SpaceAdministrator)
            throw new DomainException("Визнати неявку може лише адміністратор простору.");
        RequireStatus(UsageStatus.Expected);
        if (now < Slot.Start + Policy.NoShowGracePeriod)
            throw new DomainException("Допустимий час очікування ще не минув.");
        if (DelayWasReported)
            throw new DomainException("Неявку не визнають: організатор повідомив про затримку.");

        Status = UsageStatus.NoShow;
        events.Add(new NoShowDeclared(ReservationId, now));
    }

    public void CancelExpectation(DateTimeOffset at)
    {
        RequireStatus(UsageStatus.Expected);

        Status = UsageStatus.Cancelled;
        events.Add(new UsageExpectationCancelled(ReservationId, at));
    }

    private void RequireOrganizer(Actor actor)
    {
        if (actor.Role != UsageRole.Organizer || actor.UserId != Organizer)
            throw new DomainException("Дію може виконати лише організатор цього резервування.");
    }

    private void RequireStatus(UsageStatus expected)
    {
        if (Status != expected)
            throw new DomainException($"Очікувався стан {expected}, а використання перебуває у стані {Status}.");
    }

    public bool Equals(ZoneUsageRecord? other) => other is not null && ReservationId == other.ReservationId;
    public override bool Equals(object? obj) => Equals(obj as ZoneUsageRecord);
    public override int GetHashCode() => ReservationId.GetHashCode();
}
