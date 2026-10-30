namespace StockCore.Application.Categories;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryResponse>> ListAsync(CancellationToken cancellationToken);

    Task<CategoryResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<CategoryResponse> CreateAsync(CategoryRequest request, CancellationToken cancellationToken);

    Task<CategoryResponse> UpdateAsync(Guid id, CategoryRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
