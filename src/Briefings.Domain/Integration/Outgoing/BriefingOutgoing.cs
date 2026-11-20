using System.Text.Json;
using Briefings.Domain;

namespace Briefings.Integration;

public sealed record BriefingMessage(
    Guid MessageId,
    int Version,
    Guid CorrelationId,
    Guid ReservationId,
    Guid ZoneId,
    Guid PersonId,
    Guid OfficerId,
    long Revision,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset ValidFromUtc,
    DateTimeOffset ValidUntilUtc,
    string StateCode);

public sealed class BriefingNotificationPublisher
{
    public BriefingMessage? Pending { get; private set; }

    public void Prepare(
        BriefingRecord record,
        Guid reservationId,
        Guid zoneId,
        Guid officerId,
        long revision,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (Pending is not null)
            throw new InvalidOperationException("Попереднє інтеграційне повідомлення ще не підтверджено.");

        if (reservationId == Guid.Empty || zoneId == Guid.Empty || officerId == Guid.Empty || revision <= 0)
            throw new DomainException("Некоректні параметри для формування інтеграційного повідомлення.");

        var validity = record.EffectiveValidity()
            ?? throw new DomainException("Неможливо підготувати повідомлення без чинного інструктажу.");

        Pending = new BriefingMessage(
            Guid.NewGuid(),
            1,
            reservationId,
            reservationId,
            zoneId,
            record.Person.Value,
            officerId,
            revision,
            now.ToUniversalTime(),
            validity.From.ToUniversalTime(),
            validity.Until.ToUniversalTime(),
            "RECORDED");
    }

    public string PendingJson() =>
        JsonSerializer.Serialize(Pending
            ?? throw new InvalidOperationException("Немає підготовленого повідомлення для серіалізації."));

    public void Acknowledge(Guid messageId)
    {
        if (Pending?.MessageId != messageId)
            throw new InvalidOperationException("Ідентифікатор підтвердження не збігається з очікуваним повідомленням.");

        Pending = null;
    }
}
