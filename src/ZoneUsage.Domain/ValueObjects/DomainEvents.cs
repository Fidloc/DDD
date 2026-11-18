namespace ZoneUsage.Domain;

public abstract record DomainEvent(ReservationId ReservationId, DateTimeOffset OccurredAt);

public sealed record UsageStarted(ReservationId ReservationId, DateTimeOffset OccurredAt)
    : DomainEvent(ReservationId, OccurredAt);

public sealed record UsageCompleted(ReservationId ReservationId, bool Early, DateTimeOffset OccurredAt)
    : DomainEvent(ReservationId, OccurredAt);

public sealed record DelayReported(ReservationId ReservationId, DateTimeOffset OccurredAt)
    : DomainEvent(ReservationId, OccurredAt);

public sealed record NoShowDeclared(ReservationId ReservationId, DateTimeOffset OccurredAt)
    : DomainEvent(ReservationId, OccurredAt);

public sealed record UsageExpectationCancelled(ReservationId ReservationId, DateTimeOffset OccurredAt)
    : DomainEvent(ReservationId, OccurredAt);
