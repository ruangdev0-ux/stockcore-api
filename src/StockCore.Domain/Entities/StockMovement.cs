using StockCore.Domain.Enums;
using StockCore.Domain.Rules;

namespace StockCore.Domain.Entities;

/// <summary>
/// Registro imutável de uma alteração de saldo. Movimentos nunca são apagados:
/// um erro é corrigido com o cancelamento, que gera um estorno.
/// </summary>
public sealed class StockMovement
{
    private StockMovement()
    {
    }

    private StockMovement(
        Guid productId,
        MovementType type,
        int quantity,
        decimal? unitCost,
        string? note,
        int balanceAfter,
        Guid createdById,
        DateTime createdAt,
        Guid? reversalOfId = null)
    {
        StockRules.EnsurePositiveQuantity(quantity);
        Id = Guid.NewGuid();
        ProductId = productId;
        Type = type;
        Quantity = quantity;
        UnitCost = unitCost;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        Status = MovementStatus.Active;
        BalanceAfter = balanceAfter;
        CreatedById = createdById;
        CreatedAt = createdAt;
        ReversalOfId = reversalOfId;
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public Product? Product { get; private set; }

    public MovementType Type { get; private set; }

    /// <summary>Sempre positiva; o tipo diz se soma ou subtrai.</summary>
    public int Quantity { get; private set; }

    /// <summary>Custo unitário informado na entrada (opcional).</summary>
    public decimal? UnitCost { get; private set; }

    public string? Note { get; private set; }

    public MovementStatus Status { get; private set; }

    /// <summary>Saldo do produto logo depois deste movimento.</summary>
    public int BalanceAfter { get; private set; }

    /// <summary>Em um estorno, aponta para o movimento cancelado.</summary>
    public Guid? ReversalOfId { get; private set; }

    public Guid CreatedById { get; private set; }

    public User? CreatedBy { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    public Guid? CancelledById { get; private set; }

    public string? CancellationReason { get; private set; }

    internal static StockMovement CreateEntry(
        Guid productId, int quantity, decimal? unitCost, string? note, int balanceAfter, Guid userId, DateTime now) =>
        new(productId, MovementType.Entry, quantity, unitCost, note, balanceAfter, userId, now);

    internal static StockMovement CreateExit(
        Guid productId, int quantity, string? note, int balanceAfter, Guid userId, DateTime now) =>
        new(productId, MovementType.Exit, quantity, null, note, balanceAfter, userId, now);

    internal static StockMovement CreateReversal(
        StockMovement original, int balanceAfter, Guid userId, string reason, DateTime now) =>
        new(original.ProductId, MovementType.Reversal, original.Quantity, null, reason, balanceAfter, userId, now, original.Id);

    internal void MarkCancelled(Guid userId, string reason, DateTime now)
    {
        Status = MovementStatus.Cancelled;
        CancelledAt = now;
        CancelledById = userId;
        CancellationReason = reason.Trim();
    }
}
