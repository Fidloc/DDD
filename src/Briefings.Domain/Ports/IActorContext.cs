using Briefings.Domain;

namespace Briefings.Application;

public interface IActorContext
{
    Actor CurrentActor { get; }
}
