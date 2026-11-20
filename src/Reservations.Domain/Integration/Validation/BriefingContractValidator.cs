using System.Text.Json;
using System.Text.Json.Serialization;

namespace Reservations.Integration;

public static class BriefingContractValidator
{
    private static readonly string[] Fields =
    [
        "MessageId", "Version", "CorrelationId", "ReservationId",
        "ZoneId", "PersonId", "OfficerId", "Revision",
        "OccurredAtUtc", "ValidFromUtc", "ValidUntilUtc", "StateCode"
    ];

    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static BriefingRecordedV1 Parse(string json, DateTimeOffset now)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            throw new JsonException("object_required");

        var names = document.RootElement.EnumerateObject().Select(p => p.Name).ToArray();
        if (names.Length != Fields.Length ||
            names.Distinct().Count() != names.Length ||
            Fields.Any(f => !document.RootElement.TryGetProperty(f, out var value) || value.ValueKind == JsonValueKind.Null))
        {
            throw new JsonException("required_field");
        }

        var message = JsonSerializer.Deserialize<BriefingRecordedV1>(json, Options)
            ?? throw new JsonException();

        if (message.Version != 1 ||
            message.Revision <= 0 ||
            message.StateCode != "RECORDED" ||
            message.CorrelationId != message.ReservationId ||
            new[] { message.MessageId, message.ReservationId, message.ZoneId, message.PersonId, message.OfficerId }
                .Any(id => id == Guid.Empty) ||
            message.OccurredAtUtc == default ||
            message.ValidFromUtc == default ||
            message.ValidUntilUtc == default ||
            message.ValidFromUtc > message.OccurredAtUtc ||
            message.ValidUntilUtc <= message.ValidFromUtc ||
            message.OccurredAtUtc > now)
        {
            throw new JsonException("invalid_contract");
        }

        return message with
        {
            OccurredAtUtc = message.OccurredAtUtc.ToUniversalTime(),
            ValidFromUtc = message.ValidFromUtc.ToUniversalTime(),
            ValidUntilUtc = message.ValidUntilUtc.ToUniversalTime()
        };
    }

    public static string Canonical(BriefingRecordedV1 message) =>
        JsonSerializer.Serialize(message);
}

public sealed class BriefingMessageCache
{
    private readonly Dictionary<Guid, string> messages = [];
    private readonly Dictionary<(Guid PersonId, long Revision), string> revisions = [];

    public int Count => messages.Count;

    public Receipt Apply(string json, DateTimeOffset now)
    {
        var parsed = BriefingContractValidator.Parse(json, now);
        var canonical = BriefingContractValidator.Canonical(parsed);

        if (messages.TryGetValue(parsed.MessageId, out var existing))
            return existing == canonical
                ? Receipt.Duplicate
                : throw new InvalidDataException("message_conflict");

        if (revisions.ContainsKey((parsed.PersonId, parsed.Revision)))
            throw new InvalidDataException("revision_conflict");

        var current = revisions.Keys
            .Where(k => k.PersonId == parsed.PersonId)
            .Select(k => k.Revision)
            .DefaultIfEmpty(0)
            .Max();

        if (parsed.Revision != current + 1)
            throw new InvalidDataException("revision_gap_or_stale");

        messages.Add(parsed.MessageId, canonical);
        revisions.Add((parsed.PersonId, parsed.Revision), canonical);
        return Receipt.Applied;
    }
}
