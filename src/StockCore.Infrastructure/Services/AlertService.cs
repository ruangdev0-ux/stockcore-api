using Microsoft.EntityFrameworkCore;
using StockCore.Application.Alerts;
using StockCore.Infrastructure.Persistence;

namespace StockCore.Infrastructure.Services;

public sealed class AlertService(AppDbContext db) : IAlertService
{
    public async Task<IReadOnlyList<LowStockItem>> GetLowStockAsync(CancellationToken cancellationToken) =>
        await db.Products.AsNoTracking()
            .Where(p => p.IsActive && p.CurrentStock <= p.MinimumStock)
            .OrderByDescending(p => p.MinimumStock - p.CurrentStock).ThenBy(p => p.Name)
            .Select(p => new LowStockItem(
                p.Id,
                p.Sku,
                p.Name,
                p.Category!.Name,
                p.CurrentStock,
                p.MinimumStock,
                p.MinimumStock - p.CurrentStock))
            .ToListAsync(cancellationToken);
}
