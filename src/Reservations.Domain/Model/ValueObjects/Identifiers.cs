namespace Reservations.Domain;

/// <summary>Ідентифікатор зони коворкінгу (переговорна, диспетчерська кімната, робоче місце, лабораторія).</summary>
public sealed record ZoneId(Guid Value)
{
    public static ZoneId New() => new(Guid.NewGuid());
}

/// <summary>Ідентифікатор резервування. Заявка й резервування — той самий об'єкт з неперервною історією.</summary>
public sealed record ReservationId(Guid Value)
{
    public static ReservationId New() => new(Guid.NewGuid());
}

/// <summary>Ідентифікатор користувача, отриманий із контексту «Ідентифікація та доступ».</summary>
public sealed record UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());
}
