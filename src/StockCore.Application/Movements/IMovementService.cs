using StockCore.Application.Common;

namespace StockCore.Application.Movements;

public interface IMovementService
{
    Task<StockOperationResponse> RegisterEntryAsync(EntryRequest request, Guid userId, CancellationToken cancellationToken);

    Task<StockOperationResponse> RegisterExitAsync(ExitRequest request, Guid userId, CancellationToken cancellationToken);

    Task<StockOperationResponse> CancelAsync(
        Guid movementId, CancelMovementRequest request, Guid userId, CancellationToken cancellationToken);

    Task<MovementResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<MovementResponse>> ListAsync(MovementQuery query, CancellationToken cancellationToken);
}
