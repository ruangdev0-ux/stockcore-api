using Microsoft.EntityFrameworkCore;
using StockCore.Application.Reports;
using StockCore.Infrastructure.Persistence;

namespace StockCore.Infrastructure.Services;

public sealed class ReportService(AppDbContext db, TimeProvider clock) : IReportService
{
    public const int DefaultPeriodDays = 30;

    public async Task<StockSummaryResponse> GetStockSummaryAsync(CancellationToken cancellationToken)
    {
        var products = await db.Products.AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => new ProductStockSnapshot(p.CurrentStock, p.MinimumStock, p.UnitPrice))
            .ToListAsync(cancellationToken);
        return ReportCalculator.Summarize(products);
    }

    public async Task<MovementReportResponse> GetMovementReportAsync(
        MovementReportQuery query, CancellationToken cancellationToken)
    {
        var to = query.To?.UtcDateTime ?? clock.GetUtcNow().UtcDateTime;
        var from = query.From?.UtcDateTime ?? to.AddDays(-DefaultPeriodDays);

        var movements = await db.StockMovements.AsNoTracking()
            .Where(m => m.CreatedAt >= from && m.CreatedAt <= to)
            .Select(m => new MovementSnapshot(m.ProductId, m.Product!.Sku, m.Product!.Name, m.Type, m.Status, m.Quantity))
            .ToListAsync(cancellationToken);
        return ReportCalculator.SummarizeMovements(movements, from, to);
    }
}
