using StockCore.Domain.Enums;
using StockCore.Domain.Rules;

namespace StockCore.Application.Reports;

/// <summary>Cálculos dos relatórios em funções puras (testadas sem banco).</summary>
public static class ReportCalculator
{
    public static StockSummaryResponse Summarize(IEnumerable<ProductStockSnapshot> activeProducts)
    {
        var products = activeProducts.ToList();
        return new StockSummaryResponse(
            ActiveProducts: products.Count,
            TotalUnits: products.Sum(p => p.CurrentStock),
            TotalValue: products.Sum(p => p.CurrentStock * p.UnitPrice),
            LowStockProducts: products.Count(p => StockRules.IsLowStock(p.CurrentStock, p.MinimumStock)),
            OutOfStockProducts: products.Count(p => p.CurrentStock == 0));
    }

    public static MovementReportResponse SummarizeMovements(
        IEnumerable<MovementSnapshot> movements, DateTime from, DateTime to)
    {
        var list = movements.ToList();
        var effective = list.Where(m => m.Status == MovementStatus.Active).ToList();

        static MovementTotals Totals(IEnumerable<MovementSnapshot> items)
        {
            var materialized = items.ToList();
            return new MovementTotals(materialized.Count, materialized.Sum(m => m.Quantity));
        }

        var byProduct = effective
            .Where(m => m.Type != MovementType.Reversal)
            .GroupBy(m => new { m.ProductId, m.Sku, m.Name })
            .Select(g =>
            {
                var entered = g.Where(m => m.Type == MovementType.Entry).Sum(m => m.Quantity);
                var exited = g.Where(m => m.Type == MovementType.Exit).Sum(m => m.Quantity);
                return new ProductMovementTotals(g.Key.ProductId, g.Key.Sku, g.Key.Name, entered, exited, entered - exited);
            })
            .OrderBy(p => p.Sku, StringComparer.Ordinal)
            .ToList();

        return new MovementReportResponse(
            from,
            to,
            Totals(effective.Where(m => m.Type == MovementType.Entry)),
            Totals(effective.Where(m => m.Type == MovementType.Exit)),
            Totals(list.Where(m => m.Type == MovementType.Reversal)),
            byProduct);
    }
}
