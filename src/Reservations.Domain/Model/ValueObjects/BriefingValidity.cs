namespace Reservations.Domain;

/// <summary>
/// Зовнішній факт із контексту «Облік інструктажів організаторів»:
/// строк чинності інструктажу особи. Погодження лише читає цей факт і не змінює його.
/// </summary>
public sealed record BriefingValidity
{
    public UserId Person { get; }
    public DateTimeOffset ValidFrom { get; }
    public DateTimeOffset ValidUntil { get; }

    public BriefingValidity(UserId person, DateTimeOffset validFrom, DateTimeOffset validUntil)
    {
        if (validUntil <= validFrom)
            throw new DomainException("Кінець чинності інструктажу має бути пізніше за початок.");
        Person = person;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
    }

    /// <summary>Інструктаж цієї особи чинний на весь запитаний період.</summary>
    public bool CoversWholeSlot(UserId organizer, TimeSlot slot) =>
        Person == organizer && ValidFrom <= slot.Start && slot.End <= ValidUntil;
}
