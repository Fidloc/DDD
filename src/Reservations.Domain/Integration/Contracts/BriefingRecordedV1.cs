namespace Reservations.Integration;

public sealed record BriefingRecordedV1
{
    public required Guid MessageId { get; init; }
    public required int Version { get; init; }
    public required Guid CorrelationId { get; init; }
    public required Guid ReservationId { get; init; }
    public required Guid ZoneId { get; init; }
    public required Guid PersonId { get; init; }
    public required Guid OfficerId { get; init; }
    public required long Revision { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
    public required DateTimeOffset ValidFromUtc { get; init; }
    public required DateTimeOffset ValidUntilUtc { get; init; }
    public required string StateCode { get; init; }
}

public enum Receipt
{
    Applied,
    Duplicate
}
