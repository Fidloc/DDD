namespace Briefings.Application;

public interface IClock
{
    DateTimeOffset Now { get; }
}
