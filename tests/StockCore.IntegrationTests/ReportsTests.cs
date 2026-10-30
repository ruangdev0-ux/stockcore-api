using StockCore.Application.Alerts;
using StockCore.Application.Reports;
using StockCore.Domain.Exceptions;

namespace StockCore.IntegrationTests;

public class ReportsTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Alerta_lista_so_produtos_ativos_no_minimo_ou_abaixo_do_maior_deficit_para_o_menor()
    {
        var ok = await Admin.CreateStockedProductAsync(stock: 10, minimumStock: 2, sku: "OK-1");
        var atLimit = await Admin.CreateStockedProductAsync(stock: 5, minimumStock: 5, sku: "LIM-1");
        var empty = await Admin.CreateStockedProductAsync(stock: 0, minimumStock: 4, sku: "ZER-1");
        var inactive = await Admin.CreateStockedProductAsync(stock: 0, minimumStock: 9, sku: "INA-1");
        await Admin.PatchAsync($"/api/products/{inactive.Id}/deactivate", null);

        var alerts = await Viewer.GetAsAsync<List<LowStockItem>>("/api/alerts/low-stock");

        alerts.Select(a => a.ProductId).Should().Equal(empty.Id, atLimit.Id);
        alerts[0].Shortage.Should().Be(4);
        alerts.Should().NotContain(a => a.ProductId == ok.Id);
    }

    [Fact]
    public async Task Resumo_do_estoque_soma_unidades_e_valor_dos_ativos()
    {
        var category = await Admin.CreateCategoryAsync();
        var a = await Admin.CreateProductAsync(category.Id, "RES-1", minimumStock: 2, unitPrice: 2.50m);
        var b = await Admin.CreateProductAsync(category.Id, "RES-2", minimumStock: 10, unitPrice: 10m);
        var inactive = await Admin.CreateProductAsync(category.Id, "RES-3", unitPrice: 100m);
        await Admin.EntryAsync(a.Id, 10);
        await Admin.EntryAsync(b.Id, 3);
        await Admin.EntryAsync(inactive.Id, 50);
        await Admin.PatchAsync($"/api/products/{inactive.Id}/deactivate", null);

        var summary = await Viewer.GetAsAsync<StockSummaryResponse>("/api/reports/stock-summary");

        summary.Should().Be(new StockSummaryResponse(
            ActiveProducts: 2, TotalUnits: 13, TotalValue: 55m, LowStockProducts: 1, OutOfStockProducts: 0));
    }

    [Fact]
    public async Task Relatorio_de_movimentacoes_do_periodo()
    {
        var product = await Admin.CreateStockedProductAsync(stock: 10, sku: "REL-1");
        await Operator.ExitAsync(product.Id, 2);
        var wrong = await Operator.ExitAsync(product.Id, 5);
        await Admin.PostCancelAsync(wrong.Movement.Id);

        var report = await Viewer.GetAsAsync<MovementReportResponse>("/api/reports/movements");

        report.Entries.Should().Be(new MovementTotals(1, 10));
        report.Exits.Should().Be(new MovementTotals(1, 2));
        report.Reversals.Should().Be(new MovementTotals(1, 5));
        report.ByProduct.Should().ContainSingle()
            .Which.Should().Be(new ProductMovementTotals(product.Id, "REL-1", product.Name, 10, 2, 8));
    }

    [Fact]
    public async Task Relatorio_com_periodo_invalido_devolve_400()
    {
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.ToString("O"));
        var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-2).ToString("O"));

        var response = await Viewer.GetAsync($"/api/reports/movements?from={from}&to={to}");

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError);
    }
}
