namespace Reservations.Domain;

/// <summary>Ідентифікатор зони коворкінгу (переговорна, диспетчерська кімната, робоче місце, лабораторія).</summary>
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

/// <summary>Ідентифікатор резервування. Заявка й резервування — той самий об'єкт з неперервною історією.</summary>
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

/// <summary>Ідентифікатор користувача, отриманий із контексту «Ідентифікація та доступ».</summary>
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
