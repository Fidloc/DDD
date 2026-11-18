using Reservations.Domain;

namespace Reservations.Application;

public interface IActorContext
{
    Actor CurrentActor { get; }
}
