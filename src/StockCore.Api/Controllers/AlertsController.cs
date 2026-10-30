using Microsoft.AspNetCore.Mvc;
using StockCore.Application.Alerts;

namespace StockCore.Api.Controllers;

[Route("api/alerts")]
public sealed class AlertsController(IAlertService alerts) : ApiControllerBase
{
    /// <summary>Produtos ativos com saldo menor ou igual ao estoque mínimo, do maior déficit para o menor.</summary>
    [HttpGet("low-stock")]
    [ProducesResponseType<IReadOnlyList<LowStockItem>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<LowStockItem>> LowStock(CancellationToken cancellationToken) =>
        alerts.GetLowStockAsync(cancellationToken);
}
