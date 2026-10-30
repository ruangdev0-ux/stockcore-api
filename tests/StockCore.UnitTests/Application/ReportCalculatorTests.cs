using StockCore.Application.Reports;
using StockCore.Domain.Enums;

namespace StockCore.UnitTests.Application;

public class ReportCalculatorTests
{
    [Fact]
    public void Resumo_soma_unidades_e_valor_e_conta_alertas()
    {
        var products = new[]
        {
            new ProductStockSnapshot(CurrentStock: 10, MinimumStock: 5, UnitPrice: 2.50m),
            new ProductStockSnapshot(CurrentStock: 3, MinimumStock: 3, UnitPrice: 10m),
            new ProductStockSnapshot(CurrentStock: 0, MinimumStock: 1, UnitPrice: 99.90m),
        };

        var summary = ReportCalculator.Summarize(products);

        summary.ActiveProducts.Should().Be(3);
        summary.TotalUnits.Should().Be(13);
        summary.TotalValue.Should().Be(55m);
        summary.LowStockProducts.Should().Be(2);
        summary.OutOfStockProducts.Should().Be(1);
    }

    [Fact]
    public void Resumo_sem_produtos_devolve_zeros()
    {
        var summary = ReportCalculator.Summarize([]);

        summary.Should().Be(new StockSummaryResponse(0, 0, 0m, 0, 0));
    }

    [Fact]
    public void Relatorio_de_movimentos_ignora_cancelados_e_conta_estornos_a_parte()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var movements = new[]
        {
            new MovementSnapshot(a, "A-1", "Produto A", MovementType.Entry, MovementStatus.Active, 10),
            new MovementSnapshot(a, "A-1", "Produto A", MovementType.Exit, MovementStatus.Active, 3),
            new MovementSnapshot(a, "A-1", "Produto A", MovementType.Exit, MovementStatus.Cancelled, 5),
            new MovementSnapshot(a, "A-1", "Produto A", MovementType.Reversal, MovementStatus.Active, 5),
            new MovementSnapshot(b, "B-1", "Produto B", MovementType.Entry, MovementStatus.Active, 7),
        };
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddDays(30);

        var report = ReportCalculator.SummarizeMovements(movements, from, to);

        report.Entries.Should().Be(new MovementTotals(2, 17));
        report.Exits.Should().Be(new MovementTotals(1, 3));
        report.Reversals.Should().Be(new MovementTotals(1, 5));
        report.ByProduct.Should().HaveCount(2);
        report.ByProduct[0].Should().Be(new ProductMovementTotals(a, "A-1", "Produto A", 10, 3, 7));
        report.ByProduct[1].Net.Should().Be(7);
    }
}
