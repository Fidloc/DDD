namespace Reservations.Domain;

public sealed class DomainException(string message) : Exception(message);
