using StockCore.Application.Common;
using StockCore.Domain.Enums;

namespace StockCore.Application.Movements;

public sealed record EntryRequest(Guid ProductId, int Quantity, decimal? UnitCost, string? Note);

public sealed record ExitRequest(Guid ProductId, int Quantity, string? Note);

public sealed record CancelMovementRequest(string Reason);

public sealed record MovementResponse(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    MovementType Type,
    int Quantity,
    decimal? UnitCost,
    string? Note,
    MovementStatus Status,
    int BalanceAfter,
    Guid? ReversalOfId,
    Guid CreatedById,
    string CreatedByName,
    DateTime CreatedAt,
    DateTime? CancelledAt,
    Guid? CancelledById,
    string? CancellationReason);

/// <summary>Resultado de uma entrada, saída ou cancelamento, com o saldo atualizado.</summary>
public sealed record StockOperationResponse(
    MovementResponse Movement,
    int CurrentStock,
    int MinimumStock,
    bool LowStockAlert);

public sealed class MovementQuery : PageQuery
{
    public Guid? ProductId { get; set; }

    public MovementType? Type { get; set; }

    public MovementStatus? Status { get; set; }

    /// <summary>Início do período (data/hora ISO 8601, inclusivo).</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>Fim do período (data/hora ISO 8601, inclusivo).</summary>
    public DateTimeOffset? To { get; set; }
}
