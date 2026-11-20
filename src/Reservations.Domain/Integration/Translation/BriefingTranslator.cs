using Reservations.Domain;

namespace Reservations.Integration;

public static class BriefingTranslator
{
    public static BriefingValidity ToLocal(BriefingRecordedV1 message) =>
        new(new UserId(message.PersonId), message.ValidFromUtc, message.ValidUntilUtc);
}
