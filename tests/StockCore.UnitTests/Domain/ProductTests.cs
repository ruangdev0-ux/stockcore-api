using StockCore.Domain.Entities;
using StockCore.Domain.Enums;
using StockCore.Domain.Exceptions;

namespace StockCore.UnitTests.Domain;

public class ProductTests
{
    private static readonly DateTime Now = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();

    private static Product NewProduct(int minimumStock = 2) =>
        new(" beb-001 ", "Água 500 ml", null, Guid.NewGuid(), null, 2.50m, minimumStock, Now);

    [Fact]
    public void Produto_novo_comeca_ativo_com_saldo_zero_e_sku_normalizado()
    {
        var product = NewProduct();

        product.CurrentStock.Should().Be(0);
        product.IsActive.Should().BeTrue();
        product.Sku.Should().Be("BEB-001");
    }

    [Fact]
    public void Entrada_e_saida_atualizam_o_saldo_e_registram_o_saldo_apos()
    {
        var product = NewProduct();

        var entry = product.RegisterEntry(10, 1.20m, "Compra", UserId, Now);
        var exit = product.RegisterExit(4, null, UserId, Now);

        product.CurrentStock.Should().Be(6);
        entry.Type.Should().Be(MovementType.Entry);
        entry.BalanceAfter.Should().Be(10);
        exit.Type.Should().Be(MovementType.Exit);
        exit.BalanceAfter.Should().Be(6);
    }

    [Fact]
    public void Saida_maior_que_o_saldo_nao_altera_o_produto()
    {
        var product = NewProduct();
        product.RegisterEntry(3, null, null, UserId, Now);

        var act = () => product.RegisterExit(5, null, UserId, Now);

        act.Should().Throw<BusinessRuleException>();
        product.CurrentStock.Should().Be(3);
    }

    [Fact]
    public void Cancelar_saida_devolve_o_saldo_e_gera_estorno_ligado_ao_original()
    {
        var product = NewProduct();
        product.RegisterEntry(10, null, null, UserId, Now);
        var exit = product.RegisterExit(4, null, UserId, Now);

        var reversal = product.CancelMovement(exit, UserId, "Lançado errado", Now);

        product.CurrentStock.Should().Be(10);
        exit.Status.Should().Be(MovementStatus.Cancelled);
        exit.CancellationReason.Should().Be("Lançado errado");
        exit.CancelledById.Should().Be(UserId);
        reversal.Type.Should().Be(MovementType.Reversal);
        reversal.ReversalOfId.Should().Be(exit.Id);
        reversal.Quantity.Should().Be(4);
        reversal.BalanceAfter.Should().Be(10);
    }

    [Fact]
    public void Cancelar_entrada_ja_consumida_e_recusado()
    {
        var product = NewProduct();
        var entry = product.RegisterEntry(5, null, null, UserId, Now);
        product.RegisterExit(4, null, UserId, Now);

        var act = () => product.CancelMovement(entry, UserId, "Teste", Now);

        act.Should().Throw<BusinessRuleException>();
        product.CurrentStock.Should().Be(1);
        entry.Status.Should().Be(MovementStatus.Active);
    }

    [Fact]
    public void Produto_inativo_nao_movimenta_mas_aceita_cancelamento()
    {
        var product = NewProduct();
        var entry = product.RegisterEntry(5, null, null, UserId, Now);
        product.Deactivate(Now);

        var exit = () => product.RegisterExit(1, null, UserId, Now);
        exit.Should().Throw<BusinessRuleException>();

        product.CancelMovement(entry, UserId, "Correção", Now);
        product.CurrentStock.Should().Be(0);
    }

    [Fact]
    public void Alerta_de_estoque_baixo_acompanha_o_saldo()
    {
        var product = NewProduct(minimumStock: 5);
        product.RegisterEntry(8, null, null, UserId, Now);
        product.IsLowStock.Should().BeFalse();

        product.RegisterExit(3, null, UserId, Now);
        product.IsLowStock.Should().BeTrue();
    }

    [Fact]
    public void Preco_negativo_e_recusado_no_dominio()
    {
        var act = () => new Product("SKU-1", "Produto", null, Guid.NewGuid(), null, -1m, 0, Now);

        act.Should().Throw<BusinessRuleException>();
    }
}
