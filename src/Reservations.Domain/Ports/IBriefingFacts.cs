using Reservations.Domain;

namespace Reservations.Application;

public interface IBriefingFacts
{
    Task<BriefingValidity?> GetValidityAsync(UserId organizer);
}
