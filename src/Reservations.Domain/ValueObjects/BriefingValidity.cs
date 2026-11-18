namespace Reservations.Domain;

//Зовнішній факт із контексту «Облік інструктажів організаторів»

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

    //Інструктаж цієї особи чинний на весь запитаний період
    public bool CoversWholeSlot(UserId organizer, TimeSlot slot) =>
        Person == organizer && ValidFrom <= slot.Start && slot.End <= ValidUntil;
}
