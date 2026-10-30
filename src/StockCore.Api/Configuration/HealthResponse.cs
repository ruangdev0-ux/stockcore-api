using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace StockCore.Api.Configuration;

public static class HealthResponse
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Resposta de /health: status geral e de cada checagem (hoje, só o banco).</summary>
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new { status = entry.Value.Status.ToString(), description = entry.Value.Description }),
        };
        return context.Response.WriteAsJsonAsync(body, JsonOptions, context.RequestAborted);
    }
}
