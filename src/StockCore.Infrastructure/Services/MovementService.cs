using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using StockCore.Application.Common;
using StockCore.Application.Movements;
using StockCore.Domain.Entities;
using StockCore.Domain.Exceptions;
using StockCore.Infrastructure.Persistence;

namespace StockCore.Infrastructure.Services;

/// <summary>
/// Entradas, saídas e cancelamentos. Cada operação roda em uma transação que
/// trava a linha do produto (SELECT ... FOR UPDATE): operações simultâneas no
/// mesmo produto esperam a anterior terminar e leem o saldo já atualizado.
/// </summary>
public sealed class MovementService(AppDbContext db, TimeProvider clock) : IMovementService
{
    internal static readonly Expression<Func<StockMovement, MovementResponse>> Projection = m => new MovementResponse(
        m.Id,
        m.ProductId,
        m.Product!.Sku,
        m.Product!.Name,
        m.Type,
        m.Quantity,
        m.UnitCost,
        m.Note,
        m.Status,
        m.BalanceAfter,
        m.ReversalOfId,
        m.CreatedById,
        m.CreatedBy!.Name,
        m.CreatedAt,
        m.CancelledAt,
        m.CancelledById,
        m.CancellationReason);

    public Task<StockOperationResponse> RegisterEntryAsync(
        EntryRequest request, Guid userId, CancellationToken cancellationToken) =>
        ExecuteAsync(
            request.ProductId,
            (product, now) => product.RegisterEntry(request.Quantity, request.UnitCost, request.Note, userId, now),
            cancellationToken);

    public Task<StockOperationResponse> RegisterExitAsync(
        ExitRequest request, Guid userId, CancellationToken cancellationToken) =>
        ExecuteAsync(
            request.ProductId,
            (product, now) => product.RegisterExit(request.Quantity, request.Note, userId, now),
            cancellationToken);

    public async Task<StockOperationResponse> CancelAsync(
        Guid movementId, CancelMovementRequest request, Guid userId, CancellationToken cancellationToken)
    {
        var productId = await db.StockMovements.AsNoTracking()
            .Where(m => m.Id == movementId)
            .Select(m => (Guid?)m.ProductId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw MovementNotFound();

        return await ExecuteAsync(
            productId,
            async (product, now) =>
            {
                // Lido depois da trava: se outro cancelamento terminou antes, o status já vem "Cancelled".
                var movement = await db.StockMovements.SingleAsync(m => m.Id == movementId, cancellationToken);
                return product.CancelMovement(movement, userId, request.Reason, now);
            },
            cancellationToken);
    }

    public async Task<MovementResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await db.StockMovements.AsNoTracking().Where(m => m.Id == id).Select(Projection).SingleOrDefaultAsync(cancellationToken)
        ?? throw MovementNotFound();

    public async Task<PagedResult<MovementResponse>> ListAsync(MovementQuery query, CancellationToken cancellationToken)
    {
        var movements = db.StockMovements.AsNoTracking();

        if (query.ProductId is { } productId)
        {
            movements = movements.Where(m => m.ProductId == productId);
        }

        if (query.Type is { } type)
        {
            movements = movements.Where(m => m.Type == type);
        }

        if (query.Status is { } status)
        {
            movements = movements.Where(m => m.Status == status);
        }

        if (query.From is { } from)
        {
            var fromUtc = from.UtcDateTime;
            movements = movements.Where(m => m.CreatedAt >= fromUtc);
        }

        if (query.To is { } to)
        {
            var toUtc = to.UtcDateTime;
            movements = movements.Where(m => m.CreatedAt <= toUtc);
        }

        var total = await movements.CountAsync(cancellationToken);
        var items = await movements
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Page(query)
            .Select(Projection)
            .ToListAsync(cancellationToken);
        return new PagedResult<MovementResponse>(items, query.Page, query.PageSize, total);
    }

    private Task<StockOperationResponse> ExecuteAsync(
        Guid productId, Func<Product, DateTime, StockMovement> operation, CancellationToken cancellationToken) =>
        ExecuteAsync(productId, (product, now) => Task.FromResult(operation(product, now)), cancellationToken);

    private async Task<StockOperationResponse> ExecuteAsync(
        Guid productId, Func<Product, DateTime, Task<StockMovement>> operation, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var product = await LockProductAsync(productId, cancellationToken);
        var movement = await operation(product, clock.GetUtcNow().UtcDateTime);
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var response = await GetAsync(movement.Id, cancellationToken);
        return new StockOperationResponse(response, product.CurrentStock, product.MinimumStock, product.IsLowStock);
    }

    private async Task<Product> LockProductAsync(Guid productId, CancellationToken cancellationToken)
    {
        // Trava a linha até o fim da transação; a leitura seguinte já enxerga o saldo mais recente.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM products WHERE id = {productId} FOR UPDATE", cancellationToken);
        return await db.Products.SingleOrDefaultAsync(p => p.Id == productId, cancellationToken)
            ?? throw new NotFoundException("Produto não encontrado.");
    }

    private static NotFoundException MovementNotFound() => new("Movimentação não encontrada.");
}
