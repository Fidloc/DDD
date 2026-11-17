namespace Reservations.Domain;

/// <summary>Роль, підтверджена контекстом «Ідентифікація та доступ».</summary>
public enum ReservationRole
{
    Organizer,           // організатор
    ZoneManager,         // розпорядник зони
    SpaceAdministrator   // адміністратор простору
}

/// <summary>Хто виконує команду: користувач і його роль.</summary>
public sealed record Actor(UserId UserId, ReservationRole Role);
