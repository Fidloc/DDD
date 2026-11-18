namespace Reservations.Domain;

public enum ZoneCategory
{
    MeetingRoom,           // переговорна
    DispatchTrainingRoom,  // диспетчерська навчальна кімната
    FlexibleDesk,          // гнучке робоче місце
    Laboratory             // лабораторія
}

public enum ReservationPurpose
{
    Meeting,          // нарада
    Training,         // навчання
    IndividualWork,   // індивідуальна робота
    LaboratoryWork    // лабораторні роботи
}

// Опис зони
public sealed record ZoneProfile
{
    public ZoneCategory Category { get; }
    public int Capacity { get; }

    public ZoneProfile(ZoneCategory category, int capacity)
    {
        if (capacity < 1)
            throw new DomainException("Місткість зони має бути не меншою за 1.");
        Category = category;
        Capacity = capacity;
    }

    //Умова використання лабораторіями
    public bool RequiresBriefing => Category == ZoneCategory.Laboratory;

    public bool Allows(ReservationPurpose purpose) => Category switch
    {
        ZoneCategory.MeetingRoom => purpose is ReservationPurpose.Meeting or ReservationPurpose.Training,
        ZoneCategory.DispatchTrainingRoom => purpose is ReservationPurpose.Training or ReservationPurpose.Meeting,
        ZoneCategory.FlexibleDesk => purpose is ReservationPurpose.IndividualWork,
        ZoneCategory.Laboratory => purpose is ReservationPurpose.LaboratoryWork or ReservationPurpose.Training,
        _ => false
    };
}
