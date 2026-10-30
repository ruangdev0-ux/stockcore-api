using StockCore.Application.Common;

namespace StockCore.Application.Products;

public interface IProductService
{
    Task<PagedResult<ProductResponse>> ListAsync(ProductQuery query, CancellationToken cancellationToken);

    Task<ProductResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken);

    Task<ProductResponse> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken);

    Task<ProductResponse> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
