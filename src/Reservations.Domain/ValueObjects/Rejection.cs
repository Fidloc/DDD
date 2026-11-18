namespace Reservations.Domain;

public enum RejectionCode
{
    Conflict,          // конфлікт резервувань
    PurposeMismatch,   // зона не відповідає меті
    CapacityExceeded,  // перевищено місткість
    BriefingNotValid,  // інструктаж не чинний на весь період
    ManagerDecision    // інша причина, яку вказав розпорядник
}

//Зафіксована причина відхилення заявки
public sealed record Rejection
{
    public RejectionCode Code { get; }
    public string Details { get; }

    public Rejection(RejectionCode code, string details)
    {
        if (string.IsNullOrWhiteSpace(details))
            throw new DomainException("Причину відхилення потрібно зазначити.");
        Code = code;
        Details = details;
    }
}
