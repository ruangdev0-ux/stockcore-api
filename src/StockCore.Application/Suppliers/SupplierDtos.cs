using StockCore.Application.Common;

namespace StockCore.Application.Suppliers;

public sealed record SupplierRequest(string Name, string? Document, string? Email, string? Phone);

public sealed record SupplierResponse(
    Guid Id,
    string Name,
    string? Document,
    string? Email,
    string? Phone,
    bool IsActive,
    DateTime CreatedAt);

public sealed class SupplierQuery : PageQuery
{
    /// <summary>Busca no nome ou no documento.</summary>
    public string? Search { get; set; }

    public bool? IsActive { get; set; }
}
