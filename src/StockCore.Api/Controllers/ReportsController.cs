using Microsoft.AspNetCore.Mvc;
using StockCore.Application.Reports;

namespace StockCore.Api.Controllers;

[Route("api/reports")]
public sealed class ReportsController(IReportService reports) : ApiControllerBase
{
    /// <summary>Resumo dos produtos ativos: unidades, valor total (saldo x preço) e produtos abaixo do mínimo.</summary>
    [HttpGet("stock-summary")]
    [ProducesResponseType<StockSummaryResponse>(StatusCodes.Status200OK)]
    public Task<StockSummaryResponse> StockSummary(CancellationToken cancellationToken) =>
        reports.GetStockSummaryAsync(cancellationToken);

    /// <summary>
    /// Totais de entradas, saídas e estornos no período (from e to em ISO 8601; padrão: últimos 30 dias),
    /// também agrupados por produto.
    /// </summary>
    [HttpGet("movements")]
    [ProducesResponseType<MovementReportResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<MovementReportResponse> Movements([FromQuery] MovementReportQuery query, CancellationToken cancellationToken) =>
        reports.GetMovementReportAsync(query, cancellationToken);
}
