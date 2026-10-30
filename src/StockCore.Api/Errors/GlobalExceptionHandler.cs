using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockCore.Domain.Exceptions;

namespace StockCore.Api.Errors;

/// <summary>Converte exceções em respostas ProblemDetails com status e código adequados.</summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            AppException app => ProblemFactory.Create(httpContext, StatusFor(app), app.Code, app.Message, app.Details),

            // Proteções do banco, caso duas requisições passem juntas pelas checagens da aplicação.
            DbUpdateConcurrencyException => ProblemFactory.Create(
                httpContext,
                StatusCodes.Status409Conflict,
                ErrorCodes.Conflict,
                "O registro foi alterado por outra operação. Recarregue e tente novamente."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                ProblemFactory.Create(
                    httpContext,
                    StatusCodes.Status409Conflict,
                    ErrorCodes.Conflict,
                    "Já existe um registro com estes dados."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.CheckViolation } } =>
                ProblemFactory.Create(
                    httpContext,
                    StatusCodes.Status422UnprocessableEntity,
                    ErrorCodes.BusinessRuleViolation,
                    "A operação violaria uma regra do estoque."),

            BadHttpRequestException bad => ProblemFactory.Create(
                httpContext, bad.StatusCode, ErrorCodes.ValidationError, "Requisição inválida."),

            _ => null,
        };

        if (problem is null)
        {
            logger.LogError(exception, "Erro não tratado em {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            problem = ProblemFactory.Create(
                httpContext,
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalError,
                "Ocorreu um erro inesperado.");
        }

        await ProblemFactory.WriteAsync(httpContext, problem);
        return true;
    }

    private static int StatusFor(AppException exception) => exception switch
    {
        NotFoundException => StatusCodes.Status404NotFound,
        ConflictException => StatusCodes.Status409Conflict,
        UnauthorizedException => StatusCodes.Status401Unauthorized,
        BusinessRuleException => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status400BadRequest,
    };
}
