namespace StockCore.Application.Reports;

public interface IReportService
{
    Task<StockSummaryResponse> GetStockSummaryAsync(CancellationToken cancellationToken);

    Task<MovementReportResponse> GetMovementReportAsync(MovementReportQuery query, CancellationToken cancellationToken);
}
