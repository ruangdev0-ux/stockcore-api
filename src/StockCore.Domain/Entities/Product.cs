using StockCore.Domain.Exceptions;
using StockCore.Domain.Rules;

namespace StockCore.Domain.Entities;

/// <summary>
/// Produto com saldo. O saldo só muda pelos métodos de movimentação
/// (<see cref="RegisterEntry"/>, <see cref="RegisterExit"/> e <see cref="CancelMovement"/>),
/// que aplicam as regras de <see cref="StockRules"/> e devolvem o movimento a ser gravado.
/// </summary>
public sealed class Product
{
    private Product()
    {
    }

    public Product(
        string sku,
        string name,
        string? description,
        Guid categoryId,
        Guid? supplierId,
        decimal unitPrice,
        int minimumStock,
        DateTime createdAt)
    {
        Id = Guid.NewGuid();
        Sku = NormalizeSku(sku);
        IsActive = true;
        CurrentStock = 0;
        CreatedAt = createdAt;
        Update(name, description, categoryId, supplierId, unitPrice, minimumStock, createdAt);
    }

    public Guid Id { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public Guid CategoryId { get; private set; }

    public Category? Category { get; private set; }

    public Guid? SupplierId { get; private set; }

    public Supplier? Supplier { get; private set; }

    public decimal UnitPrice { get; private set; }

    public int CurrentStock { get; private set; }

    public int MinimumStock { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    /// <summary>Token de concorrência (coluna de sistema xmin do PostgreSQL).</summary>
    public uint Version { get; private set; }

    public bool IsLowStock => StockRules.IsLowStock(CurrentStock, MinimumStock);

    /// <summary>SKUs são guardados sem espaços e em maiúsculas.</summary>
    public static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();

    public void Update(
        string name,
        string? description,
        Guid categoryId,
        Guid? supplierId,
        decimal unitPrice,
        int minimumStock,
        DateTime now)
    {
        if (unitPrice < 0)
        {
            throw new BusinessRuleException("O preço não pode ser negativo.");
        }

        if (minimumStock < 0)
        {
            throw new BusinessRuleException("O estoque mínimo não pode ser negativo.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        CategoryId = categoryId;
        SupplierId = supplierId;
        UnitPrice = unitPrice;
        MinimumStock = minimumStock;
        UpdatedAt = now;
    }

    public void Activate(DateTime now)
    {
        IsActive = true;
        UpdatedAt = now;
    }

    public void Deactivate(DateTime now)
    {
        IsActive = false;
        UpdatedAt = now;
    }

    public StockMovement RegisterEntry(int quantity, decimal? unitCost, string? note, Guid userId, DateTime now)
    {
        StockRules.EnsureCanReceive(IsActive, quantity);
        CurrentStock += quantity;
        UpdatedAt = now;
        return StockMovement.CreateEntry(Id, quantity, unitCost, note, CurrentStock, userId, now);
    }

    public StockMovement RegisterExit(int quantity, string? note, Guid userId, DateTime now)
    {
        StockRules.EnsureCanRemove(IsActive, CurrentStock, quantity);
        CurrentStock -= quantity;
        UpdatedAt = now;
        return StockMovement.CreateExit(Id, quantity, note, CurrentStock, userId, now);
    }

    /// <summary>
    /// Cancela uma entrada ou saída: marca o original como cancelado, desfaz o efeito
    /// no saldo e devolve o estorno. Correções são permitidas mesmo com o produto inativo.
    /// </summary>
    public StockMovement CancelMovement(StockMovement movement, Guid userId, string reason, DateTime now)
    {
        if (movement.ProductId != Id)
        {
            throw new InvalidOperationException("A movimentação não pertence a este produto.");
        }

        StockRules.EnsureCanCancel(movement.Type, movement.Status, CurrentStock, movement.Quantity);
        CurrentStock = StockRules.BalanceAfterReversal(movement.Type, CurrentStock, movement.Quantity);
        UpdatedAt = now;
        movement.MarkCancelled(userId, reason, now);
        return StockMovement.CreateReversal(movement, CurrentStock, userId, reason, now);
    }
}
