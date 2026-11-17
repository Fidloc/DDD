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

/// <summary>
/// Опис зони, потрібний для рішення про погодження: категорія й місткість.
/// Допустимі цілі та потреба в інструктажі випливають з категорії
/// (перелік умов для інших категорій — відкрите питання Г2).
/// </summary>
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

    /// <summary>Умова використання: для лабораторії потрібен чинний інструктаж організатора.</summary>
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
