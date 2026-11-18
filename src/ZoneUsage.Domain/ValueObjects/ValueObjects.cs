namespace ZoneUsage.Domain;

public sealed record ReservationId
{
    public Guid Value { get; }

    public ReservationId(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainException("Ідентифікатор резервування не може бути порожнім.");
        Value = value;
    }

    public static ReservationId New() => new(Guid.NewGuid());
}

public sealed record ZoneId
{
    public Guid Value { get; }

    public ZoneId(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainException("Ідентифікатор зони не може бути порожнім.");
        Value = value;
    }

    public static ZoneId New() => new(Guid.NewGuid());
}

public sealed record UserId
{
    public Guid Value { get; }

    public UserId(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainException("Ідентифікатор користувача не може бути порожнім.");
        Value = value;
    }

    public static UserId New() => new(Guid.NewGuid());
}

public enum UsageRole
{
    Organizer,          
    SpaceAdministrator  
}

public sealed record Actor(UserId UserId, UsageRole Role);

public sealed record TimeSlot
{
    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }

    public TimeSlot(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
            throw new DomainException("Початок інтервалу має передувати його закінченню.");
        Start = start;
        End = end;
    }

    public bool Contains(DateTimeOffset moment) => Start <= moment && moment < End;
}

public sealed record ConfirmedSlot(ReservationId ReservationId, ZoneId ZoneId, UserId Organizer, TimeSlot Slot);

public sealed record UsagePolicy
{
    public TimeSpan NoShowGracePeriod { get; }

    public static UsagePolicy Default { get; } = new(TimeSpan.FromMinutes(15));

    public UsagePolicy(TimeSpan noShowGracePeriod)
    {
        if (noShowGracePeriod < TimeSpan.Zero)
            throw new DomainException("Інтервал очікування не може бути від'ємним.");
        NoShowGracePeriod = noShowGracePeriod;
    }
}
