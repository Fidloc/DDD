using Briefings.Domain;
using Briefings.Integration;

namespace Briefings.Messaging.Outbox;

public sealed record OutboxItem(Guid MessageId, string Json, bool Sent);

public sealed record BriefingSenderState(
    BriefingRecordState Record,
    long Revision,
    OutboxItem? Outbox);

public sealed class BriefingOutboxSender
{
    private readonly Actor trustedOfficer;

    public BriefingSenderState State { get; private set; }

    public BriefingOutboxSender(BriefingRecord initial, Actor trustedOfficer)
    {
        ArgumentNullException.ThrowIfNull(initial);
        ArgumentNullException.ThrowIfNull(trustedOfficer);

        this.trustedOfficer = trustedOfficer;
        State = new BriefingSenderState(initial.ToState(), 0, null);
    }

    public void RecordAndQueue(
        Guid reservationId,
        Guid zoneId,
        DateTimeOffset conductedAt,
        DateTimeOffset now,
        bool failBeforeCommit = false)
    {
        if (State.Outbox is { Sent: false })
            throw new InvalidOperationException("Попереднє повідомлення у вихідній скриньці ще не доставлено.");

        var record = BriefingRecord.Restore(State.Record);
        record.RecordBriefing(trustedOfficer, conductedAt, now);

        var nextRevision = State.Revision + 1;
        var publisher = new BriefingNotificationPublisher();
        publisher.Prepare(record, reservationId, zoneId, trustedOfficer.PersonId.Value, nextRevision, now);

        var next = new BriefingSenderState(
            record.ToState(),
            nextRevision,
            new OutboxItem(publisher.Pending!.MessageId, publisher.PendingJson(), false));

        if (failBeforeCommit)
            throw new IOException("simulated_sender_failure");

        State = next;
    }

    public void Deliver(Action<string> sendJson, bool loseAcknowledgement = false)
    {
        ArgumentNullException.ThrowIfNull(sendJson);

        var item = State.Outbox ?? throw new InvalidOperationException("no_message");
        if (item.Sent)
            return;

        sendJson(item.Json);

        if (loseAcknowledgement)
            throw new IOException("acknowledgement_lost");

        State = State with { Outbox = item with { Sent = true } };
    }
}
