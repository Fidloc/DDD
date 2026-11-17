namespace ZoneUsage.Application;

public interface IUnitOfWork
{
    Task SaveChangesAsync();
}
