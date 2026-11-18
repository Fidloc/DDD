namespace Briefings.Domain;

public sealed record BriefingRecordState(
    PersonId Person,
    BriefingPolicy Policy,
    IReadOnlyList<BriefingPass> Passes,
    DateTimeOffset? RevokedAt);

public sealed class BriefingRecord : IEquatable<BriefingRecord>
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
        Person = person ?? throw new DomainException("Ідентифікатор особи є обов'язковим.");
        Policy = policy ?? throw new DomainException("Політика чинності інструктажу є обов'язковою.");
    }

    private BriefingRecord(BriefingRecordState state)
    {
        Person = state.Person;
        Policy = state.Policy;
        passes.AddRange(state.Passes);
        RevokedAt = state.RevokedAt;
    }

    public static BriefingRecord Restore(BriefingRecordState state)
    {
        if (state is null)
            throw new DomainException("Стан облікової картки інструктажів не може бути порожнім.");
        if (state.Person is null)
            throw new DomainException("Ідентифікатор особи є обов'язковим для відновлення.");
        if (state.Policy is null)
            throw new DomainException("Політика інструктажів є обов'язковою для відновлення.");
        if (state.Passes is null)
            throw new DomainException("Перелік інструктажів не може бути невизначеним.");

        DateTimeOffset? previousConductedAt = null;
        foreach (var pass in state.Passes)
        {
            if (pass is null || pass.Validity is null)
                throw new DomainException("Запис про інструктаж містить неповні дані.");
            if (pass.Validity.From != pass.ConductedAt)
                throw new DomainException("Початок чинності має збігатися з датою проведення інструктажу.");
            if (previousConductedAt is { } prev && pass.ConductedAt <= prev)
                throw new DomainException("Історія інструктажів має бути впорядкована за зростанням дати проведення.");
            previousConductedAt = pass.ConductedAt;
        }

        if (state.RevokedAt is { } revokedAt)
        {
            if (state.Passes.Count == 0)
                throw new DomainException("Неможливо відновити стан припинення без жодного зафіксованого інструктажу.");
            var latest = state.Passes[^1];
            if (revokedAt < latest.ConductedAt)
                throw new DomainException("Момент припинення не може передувати проведенню останнього інструктажу.");
        }

        return new BriefingRecord(state);
    }

    public BriefingRecordState ToState() =>
        new(Person, Policy, passes.ToArray(), RevokedAt);

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

    public bool Equals(BriefingRecord? other) => other is not null && Person == other.Person;
    public override bool Equals(object? obj) => Equals(obj as BriefingRecord);
    public override int GetHashCode() => Person.GetHashCode();
}
