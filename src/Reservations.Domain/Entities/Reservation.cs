namespace Reservations.Domain;

public enum ReservationStatus
{
    Requested,   
    Confirmed,   
    Rejected,   
    Cancelled,  
    InUse,       
    Completed,   
    NoShow      
}

public sealed record ReservationState(
    ReservationId Id,
    UserId Organizer,
    TimeSlot Slot,
    ReservationPurpose Purpose,
    int Participants,
    ReservationStatus Status,
    Rejection? Rejection);

public sealed class Reservation : IEquatable<Reservation>
{
    public ReservationId Id { get; }
    public UserId Organizer { get; }
    public TimeSlot Slot { get; }
    public ReservationPurpose Purpose { get; }
    public int Participants { get; }
    public ReservationStatus Status { get; private set; }
    public Rejection? Rejection { get; private set; }

    internal Reservation(ReservationId id, UserId organizer, TimeSlot slot,
        ReservationPurpose purpose, int participants)
    {
        if (participants < 1)
            throw new DomainException("Кількість учасників має бути не меншою за 1.");
        Id = id;
        Organizer = organizer;
        Slot = slot;
        Purpose = purpose;
        Participants = participants;
        Status = ReservationStatus.Requested;
    }

    private Reservation(ReservationState state)
    {
        Id = state.Id;
        Organizer = state.Organizer;
        Slot = state.Slot;
        Purpose = state.Purpose;
        Participants = state.Participants;
        Status = state.Status;
        Rejection = state.Rejection;
    }

    internal static Reservation Restore(ReservationState state)
    {
        if (state is null)
            throw new DomainException("Стан резервування не може бути порожнім.");
        if (state.Id is null || state.Organizer is null || state.Slot is null)
            throw new DomainException("Ідентифікатор, організатор та часовий слот є обов'язковими.");
        if (state.Participants < 1)
            throw new DomainException("Кількість учасників має бути не меншою за 1.");
        if (state.Status == ReservationStatus.Rejected && state.Rejection is null)
            throw new DomainException("Відхилене резервування повинно містити причину відхилення.");
        if (state.Status != ReservationStatus.Rejected && state.Rejection is not null)
            throw new DomainException("Причина відхилення допустима лише для статусу Rejected.");

        return new Reservation(state);
    }

    public ReservationState ToState() =>
        new(Id, Organizer, Slot, Purpose, Participants, Status, Rejection);

    public bool BlocksZone => Status is ReservationStatus.Confirmed or ReservationStatus.InUse;

    internal void Confirm() => Status = ReservationStatus.Confirmed;

    internal void Reject(Rejection reason)
    {
        Status = ReservationStatus.Rejected;
        Rejection = reason;
    }

    internal void Cancel() => Status = ReservationStatus.Cancelled;
    internal void TakeIntoUse() => Status = ReservationStatus.InUse;
    internal void Complete() => Status = ReservationStatus.Completed;
    internal void MarkNoShow() => Status = ReservationStatus.NoShow;

    public bool Equals(Reservation? other) => other is not null && Id == other.Id;
    public override bool Equals(object? obj) => Equals(obj as Reservation);
    public override int GetHashCode() => Id.GetHashCode();
}
