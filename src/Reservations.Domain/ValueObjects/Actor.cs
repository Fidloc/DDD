namespace Reservations.Domain;

//Роль, підтверджена контекстом «Ідентифікація та доступ»
public enum ReservationRole
{
    Organizer,           // організатор
    ZoneManager,         // розпорядник зони
    SpaceAdministrator   // адміністратор простору
}

//Хто виконує команду користувач і його роль.
public sealed record Actor(UserId UserId, ReservationRole Role);
