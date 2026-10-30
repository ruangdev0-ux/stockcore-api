using StockCore.Application.Common;

namespace StockCore.Application.Products;

/// <summary>Todo produto começa com saldo zero; o saldo só muda por movimentações.</summary>
public sealed record CreateProductRequest(
    string Sku,
    string Name,
    string? Description,
    Guid CategoryId,
    Guid? SupplierId,
    decimal UnitPrice,
    int MinimumStock);

/// <summary>O SKU não muda depois do cadastro.</summary>
public sealed record UpdateProductRequest(
    string Name,
    string? Description,
    Guid CategoryId,
    Guid? SupplierId,
    decimal UnitPrice,
    int MinimumStock);

public sealed record ProductResponse(
    Guid Id,
    string Sku,
    string Name,
    string? Description,
    Guid CategoryId,
    string CategoryName,
    Guid? SupplierId,
    string? SupplierName,
    decimal UnitPrice,
    int CurrentStock,
    int MinimumStock,
    bool IsLowStock,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed class ProductQuery : PageQuery
{
    public static readonly IReadOnlyList<string> SortOptions =
    [
        "name",
        "-name",
        "sku",
        "-sku",
        "stock",
        "-stock",
        "price",
        "-price",
        "createdAt",
        "-createdAt",
    ];

    /// <summary>Busca no nome ou no SKU, sem diferenciar maiúsculas.</summary>
    public string? Search { get; set; }

    public Guid? CategoryId { get; set; }

    public Guid? SupplierId { get; set; }

    public bool? IsActive { get; set; }

    /// <summary>true: só produtos com saldo menor ou igual ao mínimo.</summary>
    public bool? LowStock { get; set; }

    /// <summary>Campo de ordenação; prefixo "-" para ordem decrescente.</summary>
    public string? Sort { get; set; }
}
