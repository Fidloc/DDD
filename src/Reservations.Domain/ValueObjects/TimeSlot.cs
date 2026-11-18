namespace Reservations.Domain;

// Часовий інтервал
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
