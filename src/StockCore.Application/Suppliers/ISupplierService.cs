using StockCore.Application.Common;

namespace StockCore.Application.Suppliers;

public interface ISupplierService
{
    Task<PagedResult<SupplierResponse>> ListAsync(SupplierQuery query, CancellationToken cancellationToken);

    Task<SupplierResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<SupplierResponse> CreateAsync(SupplierRequest request, CancellationToken cancellationToken);

    Task<SupplierResponse> UpdateAsync(Guid id, SupplierRequest request, CancellationToken cancellationToken);

    Task<SupplierResponse> DeactivateAsync(Guid id, CancellationToken cancellationToken);
}
