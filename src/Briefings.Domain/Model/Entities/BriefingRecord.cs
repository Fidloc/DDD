namespace Briefings.Domain;

public sealed class BriefingRecord
{
    private readonly List<BriefingPass> passes = [];
    private readonly List<DomainEvent> events = [];

    public PersonId Person { get; }
    public BriefingPolicy Policy { get; }
    public DateTimeOffset? RevokedAt { get; private set; }

    public IReadOnlyList<BriefingPass> Passes => passes.AsReadOnly();
    public IReadOnlyList<DomainEvent> Events => events.AsReadOnly();

    public BriefingRecord(PersonId person, BriefingPolicy policy)
    {
        Person = person;
        Policy = policy;
    }

    public BriefingPass? LatestPass => passes.Count == 0 ? null : passes[^1];
    
    public void RecordBriefing(Actor actor, DateTimeOffset conductedAt, DateTimeOffset now)
    {
        RequireOfficer(actor);
        if (conductedAt > now)
            throw new DomainException("Не можна зафіксувати інструктаж, який ще не проведено.");
        if (LatestPass is { } latest && conductedAt <= latest.ConductedAt)
            throw new DomainException("Новий інструктаж має бути пізнішим за останній зафіксований.");
        var validity = new ValidityPeriod(conductedAt, conductedAt + Policy.ValidFor);

        passes.Add(new BriefingPass(conductedAt, validity));
        RevokedAt = null;
        events.Add(new BriefingRecorded(Person, validity, now));
    }
    
    public void Revoke(Actor actor, string reason, DateTimeOffset now)
    {
        RequireOfficer(actor);
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Причину припинення потрібно зазначити.");
        if (EffectiveValidity() is not { } validity || !validity.Contains(now))
            throw new DomainException("Немає чинного інструктажу, який можна припинити.");

        RevokedAt = now;
        events.Add(new BriefingRevoked(Person, reason, now));
    }
    
    public ValidityPeriod? EffectiveValidity()
    {
        if (LatestPass is not { } latest)
            return null;
        if (RevokedAt is not { } revokedAt)
            return latest.Validity;
        return revokedAt <= latest.Validity.From
            ? null
            : new ValidityPeriod(latest.Validity.From, Min(revokedAt, latest.Validity.Until));
    }
    
    public bool IsValidThroughout(DateTimeOffset start, DateTimeOffset end) =>
        EffectiveValidity() is { } validity && validity.Covers(start, end);

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;

    private static void RequireOfficer(Actor actor)
    {
        if (actor.Role != BriefingRole.BriefingOfficer)
            throw new DomainException("Фіксувати інструктажі може лише відповідальна за інструктаж особа.");
    }
}
