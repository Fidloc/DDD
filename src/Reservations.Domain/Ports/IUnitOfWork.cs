namespace Reservations.Application;

public interface IUnitOfWork
{
    Task SaveChangesAsync();
}
