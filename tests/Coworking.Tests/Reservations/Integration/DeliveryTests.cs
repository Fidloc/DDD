using System.Text.Json;
using Briefings.Domain;
using Briefings.Messaging.Outbox;
using Reservations.Domain;
using Reservations.Integration;
using Reservations.Messaging.Inbox;
using Xunit;
using static Coworking.Tests.Reservations.Domain.ReservationFixture;
using BriefingActor = Briefings.Domain.Actor;

namespace Coworking.Tests.Reservations.Integration;

public sealed class DeliveryTests
{
    private static readonly BriefingActor Officer = new(PersonId.New(), BriefingRole.BriefingOfficer);

    [Fact]
    public void DeliverBriefingRecordedV1_AfterLostAcknowledgement_AppliesOnce()
    {
        var schedule = Laboratory();
        var reservationId = Submit(schedule, Tomorrow(10, 12), ReservationPurpose.LaboratoryWork);
        var sender = new BriefingOutboxSender(
            new BriefingRecord(new PersonId(OrganizerId.Value), BriefingPolicy.Default),
            Officer);
        var receiver = new ReservationInboxReceiver(schedule, Manager);

        sender.RecordAndQueue(reservationId.Value, schedule.ZoneId.Value, Now.AddDays(-10), Now);

        Receipt? firstReceipt = null;
        Assert.Throws<IOException>(() =>
            sender.Deliver(json => firstReceipt = receiver.Handle(json, Now), loseAcknowledgement: true));

        Assert.Equal(Receipt.Applied, firstReceipt);
        Assert.Equal(1, receiver.State.AppliedCount);
        Assert.False(sender.State.Outbox!.Sent);
        Assert.Equal(
            ReservationStatus.Confirmed,
            ZoneSchedule.Restore(receiver.State.Schedule).Get(reservationId).Status);

        Receipt? retryReceipt = null;
        sender.Deliver(json => retryReceipt = receiver.Handle(json, Now.AddMinutes(1)));

        Assert.Equal(Receipt.Duplicate, retryReceipt);
        Assert.Equal(1, receiver.State.AppliedCount);
        Assert.Single(receiver.State.Inbox);
        Assert.True(sender.State.Outbox!.Sent);
        Assert.Equal(
            ReservationStatus.Confirmed,
            ZoneSchedule.Restore(receiver.State.Schedule).Get(reservationId).Status);
    }

    [Fact]
    public void SenderFailureBeforeCommit_LeavesBriefingAndOutboxUnchanged()
    {
        var sender = new BriefingOutboxSender(
            new BriefingRecord(new PersonId(OrganizerId.Value), BriefingPolicy.Default),
            Officer);
        var before = sender.State;

        Assert.Throws<IOException>(() =>
            sender.RecordAndQueue(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Now.AddDays(-10),
                Now,
                failBeforeCommit: true));

        Assert.Equal(before, sender.State);
        Assert.Empty(sender.State.Record.Passes);
        Assert.Null(sender.State.Outbox);
        Assert.Equal(0L, sender.State.Revision);
    }

    [Fact]
    public void ReceiverFailureBeforeCommit_LeavesScheduleAndInboxUnchanged()
    {
        var schedule = Laboratory();
        var reservationId = Submit(schedule, Tomorrow(10, 12), ReservationPurpose.LaboratoryWork);
        var sender = new BriefingOutboxSender(
            new BriefingRecord(new PersonId(OrganizerId.Value), BriefingPolicy.Default),
            Officer);
        var receiver = new ReservationInboxReceiver(schedule, Manager);

        sender.RecordAndQueue(reservationId.Value, schedule.ZoneId.Value, Now.AddDays(-10), Now);
        var before = receiver.State;

        Assert.Throws<IOException>(() =>
            receiver.Handle(sender.State.Outbox!.Json, Now.AddMinutes(1), failBeforeCommit: true));

        Assert.Equal(before.Schedule, receiver.State.Schedule);
        Assert.Equal(
            ReservationStatus.Requested,
            ZoneSchedule.Restore(receiver.State.Schedule).Get(reservationId).Status);
        Assert.Empty(receiver.State.Inbox);
        Assert.Empty(receiver.State.Revisions);
        Assert.Equal(0, receiver.State.AppliedCount);
    }

    [Fact]
    public void ChangedPayload_UnderSameMessageId_LeavesReceiverStateUnchanged()
    {
        var schedule = Laboratory();
        var reservationId = Submit(schedule, Tomorrow(10, 12), ReservationPurpose.LaboratoryWork);
        var sender = new BriefingOutboxSender(
            new BriefingRecord(new PersonId(OrganizerId.Value), BriefingPolicy.Default),
            Officer);
        var receiver = new ReservationInboxReceiver(schedule, Manager);

        sender.RecordAndQueue(reservationId.Value, schedule.ZoneId.Value, Now.AddDays(-10), Now);
        sender.Deliver(json => receiver.Handle(json, Now));

        var before = receiver.State;
        var parsed = BriefingContractValidator.Parse(sender.State.Outbox!.Json, Now.AddMinutes(1));
        var changedJson = JsonSerializer.Serialize(parsed with
        {
            ValidUntilUtc = parsed.ValidUntilUtc.AddDays(30)
        });

        Assert.Throws<InvalidDataException>(() =>
            receiver.Handle(changedJson, Now.AddMinutes(2)));

        Assert.Equal(before.Schedule, receiver.State.Schedule);
        Assert.Equal(before.Inbox, receiver.State.Inbox);
        Assert.Equal(before.Revisions, receiver.State.Revisions);
        Assert.Equal(1, receiver.State.AppliedCount);
        Assert.Equal(
            ReservationStatus.Confirmed,
            ZoneSchedule.Restore(receiver.State.Schedule).Get(reservationId).Status);
    }
}
