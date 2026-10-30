using StockCore.Domain.Enums;
using StockCore.Domain.Exceptions;
using StockCore.Domain.Rules;

namespace StockCore.UnitTests.Domain;

public class StockRulesTests
{
    [Fact]
    public void Saida_maior_que_o_saldo_e_recusada_com_disponivel_e_solicitado()
    {
        var act = () => StockRules.EnsureCanRemove(isActive: true, available: 3, requested: 5);

        var error = act.Should().Throw<BusinessRuleException>().Which;
        error.Code.Should().Be(ErrorCodes.BusinessRuleViolation);
        error.Details!.Should().Contain("available", 3).And.Contain("requested", 5);
    }

    [Fact]
    public void Saida_igual_ao_saldo_e_permitida()
    {
        var act = () => StockRules.EnsureCanRemove(isActive: true, available: 5, requested: 5);

        act.Should().NotThrow();
    }

    [Fact]
    public void Saida_com_saldo_zero_e_recusada()
    {
        var act = () => StockRules.EnsureCanRemove(isActive: true, available: 0, requested: 1);

        act.Should().Throw<BusinessRuleException>().WithMessage("Estoque insuficiente*");
    }

    [Fact]
    public void Produto_inativo_nao_tem_saida_nem_entrada()
    {
        var exit = () => StockRules.EnsureCanRemove(isActive: false, available: 10, requested: 1);
        var entry = () => StockRules.EnsureCanReceive(isActive: false, quantity: 1);

        exit.Should().Throw<BusinessRuleException>().WithMessage("*inativo*");
        entry.Should().Throw<BusinessRuleException>().WithMessage("*inativo*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Quantidade_precisa_ser_positiva(int quantity)
    {
        var act = () => StockRules.EnsurePositiveQuantity(quantity);

        act.Should().Throw<BusinessRuleException>();
    }

    [Fact]
    public void Cancelar_entrada_sem_saldo_suficiente_e_recusado()
    {
        var act = () => StockRules.EnsureCanCancel(MovementType.Entry, MovementStatus.Active, currentStock: 2, quantity: 5);

        act.Should().Throw<BusinessRuleException>().WithMessage("*negativo*")
            .Which.Details!.Should().Contain("available", 2).And.Contain("requested", 5);
    }

    [Fact]
    public void Cancelar_saida_nao_depende_do_saldo()
    {
        var act = () => StockRules.EnsureCanCancel(MovementType.Exit, MovementStatus.Active, currentStock: 0, quantity: 5);

        act.Should().NotThrow();
    }

    [Fact]
    public void Movimento_ja_cancelado_nao_e_cancelado_de_novo()
    {
        var act = () => StockRules.EnsureCanCancel(MovementType.Exit, MovementStatus.Cancelled, currentStock: 10, quantity: 1);

        act.Should().Throw<BusinessRuleException>().WithMessage("*já foi cancelada*");
    }

    [Fact]
    public void Estorno_nao_pode_ser_cancelado()
    {
        var act = () => StockRules.EnsureCanCancel(MovementType.Reversal, MovementStatus.Active, currentStock: 10, quantity: 1);

        act.Should().Throw<BusinessRuleException>().WithMessage("*estorno*");
    }

    [Theory]
    [InlineData(MovementType.Entry, 10, 4, 6)]
    [InlineData(MovementType.Exit, 10, 4, 14)]
    public void Estorno_desfaz_o_efeito_do_movimento(MovementType type, int current, int quantity, int expected)
    {
        StockRules.BalanceAfterReversal(type, current, quantity).Should().Be(expected);
    }

    [Theory]
    [InlineData(4, 5, true)]
    [InlineData(5, 5, true)]
    [InlineData(6, 5, false)]
    [InlineData(0, 0, true)]
    public void Alerta_de_estoque_minimo_inclui_o_limite(int current, int minimum, bool expected)
    {
        StockRules.IsLowStock(current, minimum).Should().Be(expected);
    }
}
