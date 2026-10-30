using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using StockCore.Domain.Exceptions;

namespace StockCore.Api.Errors;

/// <summary>
/// Monta as respostas de erro no formato ProblemDetails (RFC 9457) com campos extras:
/// "code" (estável, para o cliente tratar), "details" ou "errors" e "traceId".
/// </summary>
public static class ProblemFactory
{
    public const string ContentType = "application/problem+json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        // Mesmo escape do MVC: acentos saem legíveis (ex.: "saída" em vez de "sa\u00EDda").
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static ProblemDetails Create(
        HttpContext context,
        int status,
        string code,
        string detail,
        IReadOnlyDictionary<string, object?>? details = null,
        IDictionary<string, string[]>? errors = null)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = TitleFor(status),
            Detail = detail,
            Instance = context.Request.Path,
        };
        problem.Extensions["code"] = code;
        if (details is not null)
        {
            problem.Extensions["details"] = details;
        }

        if (errors is not null)
        {
            problem.Extensions["errors"] = errors;
        }

        problem.Extensions["traceId"] = context.TraceIdentifier;
        return problem;
    }

    public static ProblemDetails Validation(HttpContext context, IDictionary<string, string[]> errors) =>
        Create(
            context,
            StatusCodes.Status400BadRequest,
            ErrorCodes.ValidationError,
            "Um ou mais campos são inválidos.",
            errors: errors);

    /// <summary>
    /// Resultado MVC com o problema. É um JsonResult (e não ObjectResult) para que o
    /// [Produces("application/json")] dos controllers não troque o content-type.
    /// </summary>
    public static JsonResult ToResult(ProblemDetails problem) =>
        new(problem, JsonOptions) { StatusCode = problem.Status, ContentType = ContentType };

    /// <summary>Escreve direto na resposta (usado fora do MVC: handler de exceções e eventos do JWT).</summary>
    public static Task WriteAsync(HttpContext context, ProblemDetails problem)
    {
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return context.Response.WriteAsJsonAsync(problem, JsonOptions, ContentType, context.RequestAborted);
    }

    private static string TitleFor(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Dados inválidos",
        StatusCodes.Status401Unauthorized => "Não autenticado",
        StatusCodes.Status403Forbidden => "Acesso negado",
        StatusCodes.Status404NotFound => "Recurso não encontrado",
        StatusCodes.Status409Conflict => "Conflito",
        StatusCodes.Status422UnprocessableEntity => "Regra de negócio violada",
        _ => "Erro interno",
    };
}
