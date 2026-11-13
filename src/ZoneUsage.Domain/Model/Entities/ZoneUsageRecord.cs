namespace ZoneUsage.Domain;

public enum UsageStatus
{
    Expected,    
    InProgress,  
    Completed,   
    NoShow,      
    Cancelled    
}

public sealed class ZoneUsageRecord
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

    public static ZoneUsageRecord ExpectFrom(ConfirmedSlot fact, UsagePolicy policy) => new(fact, policy);

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
}
