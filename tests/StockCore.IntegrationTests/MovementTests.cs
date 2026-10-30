using StockCore.Application.Common;
using StockCore.Application.Movements;
using StockCore.Domain.Enums;
using StockCore.Domain.Exceptions;

namespace StockCore.IntegrationTests;

public class MovementTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Entrada_saida_e_cancelamento_mantem_saldo_e_historico()
    {
        var product = await Admin.CreateStockedProductAsync(stock: 0);

        var entry = await Operator.EntryAsync(product.Id, 10);
        entry.CurrentStock.Should().Be(10);
        entry.Movement.Type.Should().Be(MovementType.Entry);
        entry.Movement.BalanceAfter.Should().Be(10);

        var exit = await Operator.ExitAsync(product.Id, 4);
        exit.CurrentStock.Should().Be(6);

        var cancel = await Admin.PostCancelAsync(exit.Movement.Id, "Saída lançada em dobro");
        await cancel.ShouldHaveStatusAsync(HttpStatusCode.Created);
        var reversal = await cancel.ReadAsAsync<StockOperationResponse>();
        reversal.CurrentStock.Should().Be(10);
        reversal.Movement.Type.Should().Be(MovementType.Reversal);
        reversal.Movement.ReversalOfId.Should().Be(exit.Movement.Id);
        reversal.Movement.Quantity.Should().Be(4);

        var original = await Viewer.GetAsAsync<MovementResponse>($"/api/movements/{exit.Movement.Id}");
        original.Status.Should().Be(MovementStatus.Cancelled);
        original.CancellationReason.Should().Be("Saída lançada em dobro");
        original.CancelledAt.Should().NotBeNull();

        var history = await Viewer.GetAsAsync<PagedResult<MovementResponse>>($"/api/products/{product.Id}/movements");
        history.TotalItems.Should().Be(3);
        history.Items.Select(m => m.Type).Should().BeEquivalentTo(
            new[] { MovementType.Entry, MovementType.Exit, MovementType.Reversal });
        (await Viewer.CurrentStockAsync(product.Id)).Should().Be(10);
    }

    [Fact]
    public async Task Saida_maior_que_o_saldo_devolve_422_com_disponivel_e_solicitado()
    {
        var product = await Admin.CreateStockedProductAsync(stock: 3);

        var response = await Operator.PostExitAsync(product.Id, 5);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.BusinessRuleViolation);
        var details = problem.GetProperty("details");
        details.GetProperty("available").GetInt32().Should().Be(3);
        details.GetProperty("requested").GetInt32().Should().Be(5);
        (await Viewer.CurrentStockAsync(product.Id)).Should().Be(3);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-2")]
    [InlineData("1.5")]
    [InlineData("\"dez\"")]
    public async Task Quantidade_que_nao_e_inteiro_positivo_devolve_400(string quantity)
    {
        var product = await Admin.CreateStockedProductAsync(stock: 10);
        using var content = new StringContent(
            $$"""{"productId":"{{product.Id}}","quantity":{{quantity}}}""", System.Text.Encoding.UTF8, "application/json");

        var response = await Operator.PostAsync("/api/movements/exits", content);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError);
        problem.GetProperty("errors").TryGetProperty("quantity", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Produto_inativo_nao_aceita_entrada_nem_saida()
    {
        var product = await Admin.CreateStockedProductAsync(stock: 5);
        await Admin.PatchAsync($"/api/products/{product.Id}/deactivate", null);

        var entry = await Operator.PostEntryAsync(product.Id, 1);
        var exit = await Operator.PostExitAsync(product.Id, 1);

        await entry.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.BusinessRuleViolation);
        await exit.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.BusinessRuleViolation);
        (await Viewer.CurrentStockAsync(product.Id)).Should().Be(5);

        await Admin.PatchAsync($"/api/products/{product.Id}/activate", null);
        await Operator.ExitAsync(product.Id, 1);
    }

    [Fact]
    public async Task Movimentar_produto_inexistente_devolve_404()
    {
        var response = await Operator.PostEntryAsync(Guid.NewGuid(), 1);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Cancelar_entrada_ja_consumida_devolve_422()
    {
        var product = await Admin.CreateStockedProductAsync(stock: 0);
        var entry = await Operator.EntryAsync(product.Id, 5);
        await Operator.ExitAsync(product.Id, 4);

        var response = await Admin.PostCancelAsync(entry.Movement.Id);

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.BusinessRuleViolation);
        problem.GetProperty("details").GetProperty("available").GetInt32().Should().Be(1);
        (await Viewer.CurrentStockAsync(product.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Nao_cancela_duas_vezes_nem_cancela_um_estorno()
    {
        var product = await Admin.CreateStockedProductAsync(stock: 10);
        var exit = await Operator.ExitAsync(product.Id, 2);
        var first = await Admin.PostCancelAsync(exit.Movement.Id);
        var reversal = await first.ReadAsAsync<StockOperationResponse>();

        var second = await Admin.PostCancelAsync(exit.Movement.Id);
        var ofReversal = await Admin.PostCancelAsync(reversal.Movement.Id);

        (await second.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.BusinessRuleViolation))
            .GetProperty("detail").GetString().Should().Contain("já foi cancelada");
        (await ofReversal.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.BusinessRuleViolation))
            .GetProperty("detail").GetString().Should().Contain("estorno");
        (await Viewer.CurrentStockAsync(product.Id)).Should().Be(10);
    }

    [Fact]
    public async Task Cancelamento_exige_motivo_e_papel_admin()
    {
        var product = await Admin.CreateStockedProductAsync(stock: 10);
        var exit = await Operator.ExitAsync(product.Id, 1);

        var byOperator = await Operator.PostCancelAsync(exit.Movement.Id);
        var withoutReason = await Admin.PostJsonAsync($"/api/movements/{exit.Movement.Id}/cancel", new { reason = "" });
        var unknown = await Admin.PostCancelAsync(Guid.NewGuid());

        await byOperator.ShouldBeProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden);
        (await withoutReason.ShouldBeProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError))
            .GetProperty("errors").TryGetProperty("reason", out _).Should().BeTrue();
        await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound);
    }

    [Fact]
    public async Task Viewer_nao_registra_movimentacoes()
    {
        var product = await Admin.CreateStockedProductAsync(stock: 10);

        await (await Viewer.PostEntryAsync(product.Id, 1)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden);
        await (await Viewer.PostExitAsync(product.Id, 1)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden);
    }

    [Fact]
    public async Task Saida_que_atinge_o_minimo_sinaliza_alerta()
    {
        var product = await Admin.CreateStockedProductAsync(stock: 10, minimumStock: 5);

        var aboveMinimum = await Operator.ExitAsync(product.Id, 4);
        var atMinimum = await Operator.ExitAsync(product.Id, 1);

        aboveMinimum.LowStockAlert.Should().BeFalse();
        atMinimum.LowStockAlert.Should().BeTrue();
        atMinimum.CurrentStock.Should().Be(5);
        atMinimum.MinimumStock.Should().Be(5);
    }

    [Fact]
    public async Task Filtros_da_listagem_de_movimentacoes()
    {
        var a = await Admin.CreateStockedProductAsync(stock: 10);
        var b = await Admin.CreateStockedProductAsync(stock: 10);
        var exit = await Operator.ExitAsync(a.Id, 2);
        await Operator.ExitAsync(b.Id, 3);
        await Admin.PostCancelAsync(exit.Movement.Id);

        var exits = await Viewer.GetAsAsync<PagedResult<MovementResponse>>("/api/movements?type=Exit");
        exits.TotalItems.Should().Be(2);

        var cancelled = await Viewer.GetAsAsync<PagedResult<MovementResponse>>("/api/movements?status=Cancelled");
        cancelled.Items.Should().ContainSingle().Which.Id.Should().Be(exit.Movement.Id);

        var ofB = await Viewer.GetAsAsync<PagedResult<MovementResponse>>($"/api/movements?productId={b.Id}");
        ofB.TotalItems.Should().Be(2);
        ofB.Items.Should().OnlyContain(m => m.ProductId == b.Id);

        var future = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));
        var none = await Viewer.GetAsAsync<PagedResult<MovementResponse>>($"/api/movements?from={future}");
        none.TotalItems.Should().Be(0);

        var paged = await Viewer.GetAsAsync<PagedResult<MovementResponse>>("/api/movements?page=1&pageSize=2");
        paged.TotalItems.Should().Be(5);
        paged.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Movimentacoes_nao_podem_ser_apagadas()
    {
        var product = await Admin.CreateStockedProductAsync(stock: 1);
        var movements = await Admin.GetAsAsync<PagedResult<MovementResponse>>($"/api/products/{product.Id}/movements");

        var response = await Admin.DeleteAsync($"/api/movements/{movements.Items[0].Id}");

        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }
}
