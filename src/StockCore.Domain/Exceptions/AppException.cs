namespace StockCore.Domain.Exceptions;

/// <summary>
/// Erro esperado da aplicação. A API converte cada tipo em um status HTTP
/// (ver GlobalExceptionHandler), sem que o domínio precise conhecer HTTP.
/// </summary>
public abstract class AppException : Exception
{
    protected AppException(string code, string message, IReadOnlyDictionary<string, object?>? details = null)
        : base(message)
    {
        Code = code;
        Details = details;
    }

    public string Code { get; }

    public IReadOnlyDictionary<string, object?>? Details { get; }
}

/// <summary>Regra de negócio violada (HTTP 422).</summary>
public sealed class BusinessRuleException(string message, IReadOnlyDictionary<string, object?>? details = null)
    : AppException(ErrorCodes.BusinessRuleViolation, message, details);

/// <summary>Recurso inexistente (HTTP 404).</summary>
public sealed class NotFoundException(string message)
    : AppException(ErrorCodes.NotFound, message);

/// <summary>Conflito com o estado atual, como SKU duplicado (HTTP 409).</summary>
public sealed class ConflictException(string message, IReadOnlyDictionary<string, object?>? details = null)
    : AppException(ErrorCodes.Conflict, message, details);

/// <summary>Credenciais inválidas (HTTP 401).</summary>
public sealed class UnauthorizedException(string message)
    : AppException(ErrorCodes.Unauthorized, message);
