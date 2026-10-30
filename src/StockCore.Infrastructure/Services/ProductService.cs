using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using StockCore.Application.Common;
using StockCore.Application.Products;
using StockCore.Domain.Entities;
using StockCore.Domain.Exceptions;
using StockCore.Infrastructure.Persistence;

namespace StockCore.Infrastructure.Services;

public sealed class ProductService(AppDbContext db, TimeProvider clock) : IProductService
{
    internal static readonly Expression<Func<Product, ProductResponse>> Projection = p => new ProductResponse(
        p.Id,
        p.Sku,
        p.Name,
        p.Description,
        p.CategoryId,
        p.Category!.Name,
        p.SupplierId,
        p.Supplier != null ? p.Supplier.Name : null,
        p.UnitPrice,
        p.CurrentStock,
        p.MinimumStock,
        p.CurrentStock <= p.MinimumStock,
        p.IsActive,
        p.CreatedAt,
        p.UpdatedAt);

    public async Task<PagedResult<ProductResponse>> ListAsync(ProductQuery query, CancellationToken cancellationToken)
    {
        var products = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = LikePattern.Contains(query.Search);
            products = products.Where(p => EF.Functions.ILike(p.Name, pattern) || EF.Functions.ILike(p.Sku, pattern));
        }

        if (query.CategoryId is { } categoryId)
        {
            products = products.Where(p => p.CategoryId == categoryId);
        }

        if (query.SupplierId is { } supplierId)
        {
            products = products.Where(p => p.SupplierId == supplierId);
        }

        if (query.IsActive is { } isActive)
        {
            products = products.Where(p => p.IsActive == isActive);
        }

        if (query.LowStock is { } lowStock)
        {
            products = lowStock
                ? products.Where(p => p.CurrentStock <= p.MinimumStock)
                : products.Where(p => p.CurrentStock > p.MinimumStock);
        }

        var total = await products.CountAsync(cancellationToken);
        var items = await Sort(products, query.Sort)
            .Page(query)
            .Select(Projection)
            .ToListAsync(cancellationToken);
        return new PagedResult<ProductResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<ProductResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Products.AsNoTracking().Where(p => p.Id == id).Select(Projection).SingleOrDefaultAsync(cancellationToken)
        ?? throw NotFound();

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var sku = Product.NormalizeSku(request.Sku);
        if (await db.Products.AnyAsync(p => p.Sku == sku, cancellationToken))
        {
            throw new ConflictException(
                "Já existe um produto com este SKU.", new Dictionary<string, object?> { ["sku"] = sku });
        }

        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        await EnsureSupplierIsActiveAsync(request.SupplierId, cancellationToken);

        var product = new Product(
            sku,
            request.Name,
            request.Description,
            request.CategoryId,
            request.SupplierId,
            request.UnitPrice,
            request.MinimumStock,
            clock.GetUtcNow().UtcDateTime);
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(product.Id, cancellationToken);
    }

    public async Task<ProductResponse> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await FindAsync(id, cancellationToken);
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        if (request.SupplierId != product.SupplierId)
        {
            await EnsureSupplierIsActiveAsync(request.SupplierId, cancellationToken);
        }

        product.Update(
            request.Name,
            request.Description,
            request.CategoryId,
            request.SupplierId,
            request.UnitPrice,
            request.MinimumStock,
            clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<ProductResponse> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken)
    {
        var product = await FindAsync(id, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        if (active)
        {
            product.Activate(now);
        }
        else
        {
            product.Deactivate(now);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await FindAsync(id, cancellationToken);
        if (await db.StockMovements.AnyAsync(m => m.ProductId == id, cancellationToken))
        {
            throw new ConflictException("O produto possui movimentações e não pode ser excluído. Inative-o.");
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<Product> Sort(IQueryable<Product> products, string? sort)
    {
        IOrderedQueryable<Product> ordered = sort switch
        {
            "-name" => products.OrderByDescending(p => p.Name),
            "sku" => products.OrderBy(p => p.Sku),
            "-sku" => products.OrderByDescending(p => p.Sku),
            "stock" => products.OrderBy(p => p.CurrentStock),
            "-stock" => products.OrderByDescending(p => p.CurrentStock),
            "price" => products.OrderBy(p => p.UnitPrice),
            "-price" => products.OrderByDescending(p => p.UnitPrice),
            "createdAt" => products.OrderBy(p => p.CreatedAt),
            "-createdAt" => products.OrderByDescending(p => p.CreatedAt),
            _ => products.OrderBy(p => p.Name),
        };
        return ordered.ThenBy(p => p.Id);
    }

    private async Task<Product> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Products.SingleOrDefaultAsync(p => p.Id == id, cancellationToken) ?? throw NotFound();

    private async Task EnsureCategoryExistsAsync(Guid categoryId, CancellationToken cancellationToken)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == categoryId, cancellationToken))
        {
            throw new BusinessRuleException("A categoria informada não existe.");
        }
    }

    private async Task EnsureSupplierIsActiveAsync(Guid? supplierId, CancellationToken cancellationToken)
    {
        if (supplierId is not { } id)
        {
            return;
        }

        var supplier = await db.Suppliers.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new { s.IsActive })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new BusinessRuleException("O fornecedor informado não existe.");

        if (!supplier.IsActive)
        {
            throw new BusinessRuleException("Fornecedor inativo não pode ser vinculado a produtos.");
        }
    }

    private static NotFoundException NotFound() => new("Produto não encontrado.");
}
