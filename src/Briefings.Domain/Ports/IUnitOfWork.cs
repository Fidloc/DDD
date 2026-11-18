namespace Briefings.Application;

public interface IUnitOfWork
{
    Task SaveChangesAsync();
}
