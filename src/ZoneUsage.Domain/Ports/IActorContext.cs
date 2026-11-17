using ZoneUsage.Domain;

namespace ZoneUsage.Application;

public interface IActorContext
{
    Actor CurrentActor { get; }
}
