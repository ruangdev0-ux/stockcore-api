using StockCore.Domain.Enums;

namespace StockCore.Application.Reports;

public sealed record StockSummaryResponse(
    int ActiveProducts,
    int TotalUnits,
    decimal TotalValue,
    int LowStockProducts,
    int OutOfStockProducts);

public sealed record MovementTotals(int Count, int Quantity);

public sealed record ProductMovementTotals(
    Guid ProductId,
    string Sku,
    string Name,
    int Entered,
    int Exited,
    int Net);

/// <summary>
/// Totais do período. Entradas e saídas contam só os movimentos ativos (não cancelados);
/// os estornos aparecem separados, para mostrar quantas correções houve.
/// </summary>
public sealed record MovementReportResponse(
    DateTime From,
    DateTime To,
    MovementTotals Entries,
    MovementTotals Exits,
    MovementTotals Reversals,
    IReadOnlyList<ProductMovementTotals> ByProduct);

public sealed record ProductStockSnapshot(int CurrentStock, int MinimumStock, decimal UnitPrice);

public sealed record MovementSnapshot(
    Guid ProductId,
    string Sku,
    string Name,
    MovementType Type,
    MovementStatus Status,
    int Quantity);

public sealed class MovementReportQuery
{
    public DateTimeOffset? From { get; set; }

    public DateTimeOffset? To { get; set; }
}
