using Microsoft.AspNetCore.Mvc;

namespace StockCore.Api.Errors;

/// <summary>
/// Erros de leitura do corpo/query antes da validação (JSON malformado, texto em campo numérico,
/// número decimal em quantidade inteira) no mesmo formato dos erros de validação.
/// </summary>
public static class InvalidModelStateResponse
{
    public static IActionResult Create(ActionContext context)
    {
        var errors = new Dictionary<string, string[]>();
        foreach (var entry in context.ModelState.Where(e => e.Value is { Errors.Count: > 0 }))
        {
            errors.TryAdd(NormalizeKey(entry.Key), ["Valor ausente ou em formato inválido."]);
        }

        if (errors.Count == 0)
        {
            errors["body"] = ["Corpo da requisição inválido."];
        }

        return ProblemFactory.ToResult(ProblemFactory.Validation(context.HttpContext, errors));
    }

    /// <summary>"$.quantity" vira "quantity"; o nome do parâmetro inteiro vira "body".</summary>
    private static string NormalizeKey(string key)
    {
        if (key.StartsWith("$.", StringComparison.Ordinal))
        {
            return key[2..];
        }

        return key is "$" or "" or "request" ? "body" : key;
    }
}
