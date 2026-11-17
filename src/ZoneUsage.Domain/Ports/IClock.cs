namespace ZoneUsage.Application;

public interface IClock
{
    DateTimeOffset Now { get; }
}
