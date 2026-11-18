namespace Reservations.Application;

public interface IClock
{
    DateTimeOffset Now { get; }
}
