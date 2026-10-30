namespace StockCore.Domain.Exceptions;

/// <summary>Códigos estáveis devolvidos no campo "code" das respostas de erro.</summary>
public static class ErrorCodes
{
    public const string ValidationError = "VALIDATION_ERROR";
    public const string NotFound = "NOT_FOUND";
    public const string BusinessRuleViolation = "BUSINESS_RULE_VIOLATION";
    public const string Conflict = "CONFLICT";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string InternalError = "INTERNAL_ERROR";
}
