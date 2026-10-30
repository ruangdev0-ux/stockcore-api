namespace StockCore.Application.Alerts;

/// <summary>Produto ativo com saldo menor ou igual ao estoque mínimo.</summary>
public sealed record LowStockItem(
    Guid ProductId,
    string Sku,
    string Name,
    string CategoryName,
    int CurrentStock,
    int MinimumStock,
    int Shortage);

public interface IAlertService
{
    Task<IReadOnlyList<LowStockItem>> GetLowStockAsync(CancellationToken cancellationToken);
}
