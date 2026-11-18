namespace Reservations.Domain;

public abstract record DomainEvent(ReservationId ReservationId, DateTimeOffset OccurredAt);

public sealed record ReservationRequested(ReservationId ReservationId, ZoneId ZoneId, UserId Organizer,
    TimeSlot Slot, DateTimeOffset OccurredAt) : DomainEvent(ReservationId, OccurredAt);

public sealed record ReservationConfirmed(ReservationId ReservationId, ZoneId ZoneId, UserId Organizer,
    TimeSlot Slot, DateTimeOffset OccurredAt) : DomainEvent(ReservationId, OccurredAt);

public sealed record ReservationRejected(ReservationId ReservationId, Rejection Reason,
    DateTimeOffset OccurredAt) : DomainEvent(ReservationId, OccurredAt);

public sealed record ReservationCancelled(ReservationId ReservationId, bool WasConfirmed,
    DateTimeOffset OccurredAt) : DomainEvent(ReservationId, OccurredAt);

public sealed record ReservationTakenIntoUse(ReservationId ReservationId,
    DateTimeOffset OccurredAt) : DomainEvent(ReservationId, OccurredAt);

public enum ReleaseReason { UsageCompleted, NoShow }

public sealed record ZoneReleased(ReservationId ReservationId, ReleaseReason Reason,
    DateTimeOffset OccurredAt) : DomainEvent(ReservationId, OccurredAt);
