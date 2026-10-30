using Microsoft.EntityFrameworkCore;
using StockCore.Application.Common;
using StockCore.Application.Suppliers;
using StockCore.Domain.Entities;
using StockCore.Domain.Exceptions;
using StockCore.Infrastructure.Persistence;

namespace StockCore.Infrastructure.Services;

public sealed class SupplierService(AppDbContext db, TimeProvider clock) : ISupplierService
{
    public async Task<PagedResult<SupplierResponse>> ListAsync(SupplierQuery query, CancellationToken cancellationToken)
    {
        var suppliers = db.Suppliers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = LikePattern.Contains(query.Search);
            suppliers = suppliers.Where(s =>
                EF.Functions.ILike(s.Name, pattern) || (s.Document != null && EF.Functions.ILike(s.Document, pattern)));
        }

        if (query.IsActive is { } isActive)
        {
            suppliers = suppliers.Where(s => s.IsActive == isActive);
        }

        var total = await suppliers.CountAsync(cancellationToken);
        var items = await suppliers
            .OrderBy(s => s.Name).ThenBy(s => s.Id)
            .Page(query)
            .Select(s => new SupplierResponse(s.Id, s.Name, s.Document, s.Email, s.Phone, s.IsActive, s.CreatedAt))
            .ToListAsync(cancellationToken);
        return new PagedResult<SupplierResponse>(items, query.Page, query.PageSize, total);
    }

    public async Task<SupplierResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ToResponse(await FindAsync(id, cancellationToken));

    public async Task<SupplierResponse> CreateAsync(SupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = new Supplier(request.Name, request.Document, request.Email, request.Phone, clock.GetUtcNow().UtcDateTime);
        await EnsureUniqueDocumentAsync(supplier.Document, null, cancellationToken);
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(supplier);
    }

    public async Task<SupplierResponse> UpdateAsync(Guid id, SupplierRequest request, CancellationToken cancellationToken)
    {
        var supplier = await FindAsync(id, cancellationToken);
        supplier.Update(request.Name, request.Document, request.Email, request.Phone);
        await EnsureUniqueDocumentAsync(supplier.Document, id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(supplier);
    }

    public async Task<SupplierResponse> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await FindAsync(id, cancellationToken);
        supplier.Deactivate();
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(supplier);
    }

    private async Task<Supplier> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Suppliers.SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
        ?? throw new NotFoundException("Fornecedor não encontrado.");

    private async Task EnsureUniqueDocumentAsync(string? document, Guid? ignoreId, CancellationToken cancellationToken)
    {
        if (document is null)
        {
            return;
        }

        if (await db.Suppliers.AnyAsync(s => s.Document == document && s.Id != ignoreId, cancellationToken))
        {
            throw new ConflictException("Já existe um fornecedor com este documento.");
        }
    }

    private static SupplierResponse ToResponse(Supplier s) =>
        new(s.Id, s.Name, s.Document, s.Email, s.Phone, s.IsActive, s.CreatedAt);
}
