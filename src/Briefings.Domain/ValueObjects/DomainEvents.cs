namespace Briefings.Domain;

public abstract record DomainEvent(PersonId Person, DateTimeOffset OccurredAt);

public sealed record BriefingRecorded(PersonId Person, ValidityPeriod Validity,
    DateTimeOffset OccurredAt) : DomainEvent(Person, OccurredAt);

public sealed record BriefingRevoked(PersonId Person, string Reason,
    DateTimeOffset OccurredAt) : DomainEvent(Person, OccurredAt);
