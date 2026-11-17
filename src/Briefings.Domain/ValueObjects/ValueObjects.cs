namespace Briefings.Domain;

public sealed record PersonId
{
    public Guid Value { get; }

    public PersonId(Guid value)
    {
        if (value == Guid.Empty)
            throw new DomainException("Ідентифікатор особи не може бути порожнім.");
        Value = value;
    }

    public static PersonId New() => new(Guid.NewGuid());
}

public enum BriefingRole
{
    Organizer,        
    BriefingOfficer   
}

public sealed record Actor(PersonId PersonId, BriefingRole Role);

public sealed record ValidityPeriod
{
    public DateTimeOffset From { get; }
    public DateTimeOffset Until { get; }

    public ValidityPeriod(DateTimeOffset from, DateTimeOffset until)
    {
        if (until <= from)
            throw new DomainException("Кінець періоду має бути пізніше за початок.");
        From = from;
        Until = until;
    }

    public bool Covers(DateTimeOffset start, DateTimeOffset end) => From <= start && end <= Until;
    public bool Contains(DateTimeOffset moment) => From <= moment && moment < Until;
}

public sealed record BriefingPass(DateTimeOffset ConductedAt, ValidityPeriod Validity);

public sealed record BriefingPolicy
{
    public TimeSpan ValidFor { get; }

    public static BriefingPolicy Default { get; } = new(TimeSpan.FromDays(365));

    public BriefingPolicy(TimeSpan validityPeriod)
    {
        if (validityPeriod <= TimeSpan.Zero)
            throw new DomainException("Строк чинності інструктажу має бути додатним.");
        ValidFor = validityPeriod;
    }
}
