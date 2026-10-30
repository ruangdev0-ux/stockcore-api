using StockCore.Application.Common;
using StockCore.Application.Movements;
using StockCore.Domain.Enums;

namespace StockCore.IntegrationTests;

/// <summary>
/// Requisições simultâneas no mesmo produto. Sem a trava da linha (SELECT ... FOR UPDATE),
/// duas saídas poderiam ler o mesmo saldo e as duas passariam.
/// </summary>
public class ConcurrencyTests(ApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Saidas_simultaneas_nunca_deixam_o_saldo_negativo()
    {
        var product = await Admin.CreateStockedProductAsync(stock: 5);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => Operator.PostExitAsync(product.Id, 1)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(5);
        responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity).Should().Be(5);
        (await Viewer.CurrentStockAsync(product.Id)).Should().Be(0);

        var exits = await Viewer.GetAsAsync<PagedResult<MovementResponse>>(
            $"/api/movements?productId={product.Id}&type=Exit");
        exits.TotalItems.Should().Be(5);
        exits.Items.Select(m => m.BalanceAfter).Should().BeEquivalentTo(new[] { 4, 3, 2, 1, 0 });
    }

    [Fact]
    public async Task Cancelamentos_simultaneos_do_mesmo_movimento_geram_um_unico_estorno()
    {
        var product = await Admin.CreateStockedProductAsync(stock: 10);
        var exit = await Operator.ExitAsync(product.Id, 3);

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => Admin.PostCancelAsync(exit.Movement.Id)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity).Should().Be(4);
        (await Viewer.CurrentStockAsync(product.Id)).Should().Be(10);

        var reversals = await Viewer.GetAsAsync<PagedResult<MovementResponse>>(
            $"/api/movements?productId={product.Id}&type={MovementType.Reversal}");
        reversals.TotalItems.Should().Be(1);
    }
}
