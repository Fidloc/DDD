namespace Briefings.Domain;

public sealed class DomainException(string message) : Exception(message);
