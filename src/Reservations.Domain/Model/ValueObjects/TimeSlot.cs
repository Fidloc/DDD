namespace Reservations.Domain;

/// <summary>
/// Часовий інтервал [Start; End). Кінець не входить до інтервалу,
/// тому бронювання впритул (12:00–13:00 і 13:00–14:00) не перетинаються.
/// Незмінний: властивості доступні лише для читання, вираз with не може їх змінити.
/// </summary>
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

    public TimeSpan Duration => End - Start;

    public bool Overlaps(TimeSlot other) => Start < other.End && other.Start < End;

    public bool Contains(DateTimeOffset moment) => Start <= moment && moment < End;
}
