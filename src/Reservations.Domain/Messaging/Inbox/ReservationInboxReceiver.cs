using Reservations.Domain;
using Reservations.Integration;

namespace Reservations.Messaging.Inbox;

public sealed record ReservationDeliveryState(
    ZoneScheduleState Schedule,
    Dictionary<Guid, string> Inbox,
    Dictionary<(Guid PersonId, long Revision), string> Revisions,
    int AppliedCount);

public sealed class ReservationInboxReceiver
{
    private readonly Actor trustedZoneManager;
    private ReservationDeliveryState state;

    public ReservationInboxReceiver(ZoneSchedule initial, Actor trustedZoneManager)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(trustedZoneManager);

        this.trustedZoneManager = trustedZoneManager;
        state = new ReservationDeliveryState(initial.ToState(), [], [], 0);
    }

    public ReservationDeliveryState State => state with
    {
        Inbox = new Dictionary<Guid, string>(state.Inbox),
        Revisions = new Dictionary<(Guid PersonId, long Revision), string>(state.Revisions)
    };

    public Receipt Handle(string json, DateTimeOffset now, bool failBeforeCommit = false)
    {
        var message = BriefingContractValidator.Parse(json, now);
        var canonical = BriefingContractValidator.Canonical(message);

        if (state.Inbox.TryGetValue(message.MessageId, out var previous))
            return previous == canonical
                ? Receipt.Duplicate
                : throw new InvalidDataException("message_conflict");

        var key = (message.PersonId, message.Revision);
        if (state.Revisions.ContainsKey(key))
            throw new InvalidDataException("revision_conflict");

        var last = state.Revisions.Keys
            .Where(k => k.PersonId == message.PersonId)
            .Select(k => k.Revision)
            .DefaultIfEmpty(0)
            .Max();

        if (message.Revision != last + 1)
            throw new InvalidDataException("revision_gap_or_stale");

        if (state.Schedule.ZoneId.Value != message.ZoneId)
            throw new InvalidDataException("wrong_zone");

        var schedule = ZoneSchedule.Restore(state.Schedule);
        schedule.Confirm(
            new ReservationId(message.ReservationId),
            trustedZoneManager,
            BriefingTranslator.ToLocal(message),
            now);

        var inbox = new Dictionary<Guid, string>(state.Inbox)
        {
            [message.MessageId] = canonical
        };
        var revisions = new Dictionary<(Guid PersonId, long Revision), string>(state.Revisions)
        {
            [key] = canonical
        };

        var next = new ReservationDeliveryState(
            schedule.ToState(),
            inbox,
            revisions,
            state.AppliedCount + 1);

        if (failBeforeCommit)
            throw new IOException("simulated_failure");

        state = next;
        return Receipt.Applied;
    }
}
