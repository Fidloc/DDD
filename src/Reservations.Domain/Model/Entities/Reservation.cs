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
