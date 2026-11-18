namespace Reservations.Domain;

//Обмеження резервування

public sealed record BookingPolicy
{
    public TimeSpan MinDuration { get; }
    public TimeSpan MaxDuration { get; }
    public TimeSpan Horizon { get; }

    public static BookingPolicy Default { get; } =
        new(TimeSpan.FromMinutes(30), TimeSpan.FromHours(8), TimeSpan.FromDays(30));

    public BookingPolicy(TimeSpan minDuration, TimeSpan maxDuration, TimeSpan horizon)
    {
        if (minDuration <= TimeSpan.Zero)
            throw new DomainException("Мінімальна тривалість має бути додатною.");
        if (maxDuration < minDuration)
            throw new DomainException("Максимальна тривалість не може бути меншою за мінімальну.");
        if (horizon <= TimeSpan.Zero)
            throw new DomainException("Горизонт бронювання має бути додатним.");
        MinDuration = minDuration;
        MaxDuration = maxDuration;
        Horizon = horizon;
    }

    //Перевіряє, чи заявку на цей інтервал взагалі можна прийняти до розгляду
    public void EnsureAllows(TimeSlot slot, DateTimeOffset now)
    {
        if (slot.Start < now)
            throw new DomainException("Не можна подати заявку на період, що вже розпочався.");
        if (slot.Duration < MinDuration)
            throw new DomainException($"Тривалість менша за мінімальну ({MinDuration}).");
        if (slot.Duration > MaxDuration)
            throw new DomainException($"Тривалість перевищує максимальну ({MaxDuration}).");
        if (slot.Start > now + Horizon)
            throw new DomainException($"Початок періоду виходить за горизонт бронювання ({Horizon.TotalDays} днів).");
    }
}
