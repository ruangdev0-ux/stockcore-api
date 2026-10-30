using Microsoft.EntityFrameworkCore;
using StockCore.Application.Categories;
using StockCore.Domain.Entities;
using StockCore.Domain.Exceptions;
using StockCore.Infrastructure.Persistence;

namespace StockCore.Infrastructure.Services;

public sealed class CategoryService(AppDbContext db, TimeProvider clock) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryResponse>> ListAsync(CancellationToken cancellationToken) =>
        await Project(db.Categories.AsNoTracking().OrderBy(c => c.Name)).ToListAsync(cancellationToken);

    public async Task<CategoryResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await Project(db.Categories.AsNoTracking().Where(c => c.Id == id)).SingleOrDefaultAsync(cancellationToken)
        ?? throw NotFound();

    public async Task<CategoryResponse> CreateAsync(CategoryRequest request, CancellationToken cancellationToken)
    {
        await EnsureUniqueNameAsync(request.Name, null, cancellationToken);
        var category = new Category(request.Name, request.Description, clock.GetUtcNow().UtcDateTime);
        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(category.Id, cancellationToken);
    }

    public async Task<CategoryResponse> UpdateAsync(Guid id, CategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken) ?? throw NotFound();
        await EnsureUniqueNameAsync(request.Name, id, cancellationToken);
        category.Update(request.Name, request.Description);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken) ?? throw NotFound();
        var productCount = await db.Products.CountAsync(p => p.CategoryId == id, cancellationToken);
        if (productCount > 0)
        {
            throw new ConflictException(
                "A categoria possui produtos e não pode ser excluída.",
                new Dictionary<string, object?> { ["productCount"] = productCount });
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<CategoryResponse> Project(IQueryable<Category> query) =>
        query.Select(c => new CategoryResponse(
            c.Id,
            c.Name,
            c.Description,
            db.Products.Count(p => p.CategoryId == c.Id),
            c.CreatedAt));

    private async Task EnsureUniqueNameAsync(string name, Guid? ignoreId, CancellationToken cancellationToken)
    {
        var normalized = name.Trim().ToLower();
        var exists = await db.Categories.AnyAsync(
            c => c.Name.ToLower() == normalized && c.Id != ignoreId, cancellationToken);
        if (exists)
        {
            throw new ConflictException("Já existe uma categoria com este nome.");
        }
    }

    private static NotFoundException NotFound() => new("Categoria não encontrada.");
}
