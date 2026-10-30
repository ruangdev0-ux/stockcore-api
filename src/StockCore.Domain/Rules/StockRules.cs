using StockCore.Domain.Enums;
using StockCore.Domain.Exceptions;

namespace StockCore.Domain.Rules;

/// <summary>
/// Regras de estoque em funções puras: recebem valores e lançam
/// <see cref="BusinessRuleException"/> quando a operação não é permitida.
/// </summary>
public static class StockRules
{
    public static void EnsurePositiveQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new BusinessRuleException("A quantidade deve ser maior que zero.");
        }
    }

    /// <summary>Produto inativo não recebe entradas.</summary>
    public static void EnsureCanReceive(bool isActive, int quantity)
    {
        EnsurePositiveQuantity(quantity);
        if (!isActive)
        {
            throw new BusinessRuleException("Produto inativo não pode receber entradas.");
        }
    }

    /// <summary>Saída só é permitida em produto ativo e até o saldo disponível.</summary>
    public static void EnsureCanRemove(bool isActive, int available, int requested)
    {
        EnsurePositiveQuantity(requested);
        if (!isActive)
        {
            throw new BusinessRuleException("Produto inativo não pode ter saídas.");
        }

        if (requested > available)
        {
            throw new BusinessRuleException(
                "Estoque insuficiente para a saída.",
                new Dictionary<string, object?> { ["available"] = available, ["requested"] = requested });
        }
    }

    /// <summary>
    /// Um movimento só pode ser cancelado uma vez, estornos não são cancelados
    /// e cancelar uma entrada não pode deixar o saldo negativo.
    /// </summary>
    public static void EnsureCanCancel(MovementType type, MovementStatus status, int currentStock, int quantity)
    {
        if (type == MovementType.Reversal)
        {
            throw new BusinessRuleException("Um estorno não pode ser cancelado.");
        }

        if (status == MovementStatus.Cancelled)
        {
            throw new BusinessRuleException("Esta movimentação já foi cancelada.");
        }

        if (type == MovementType.Entry && quantity > currentStock)
        {
            throw new BusinessRuleException(
                "Cancelar esta entrada deixaria o estoque negativo.",
                new Dictionary<string, object?> { ["available"] = currentStock, ["requested"] = quantity });
        }
    }

    /// <summary>Saldo depois do estorno: desfaz o efeito do movimento original.</summary>
    public static int BalanceAfterReversal(MovementType originalType, int currentStock, int quantity) =>
        originalType switch
        {
            MovementType.Entry => currentStock - quantity,
            MovementType.Exit => currentStock + quantity,
            _ => throw new BusinessRuleException("Um estorno não pode ser cancelado."),
        };

    /// <summary>Alerta quando o saldo chega ao estoque mínimo ou fica abaixo dele.</summary>
    public static bool IsLowStock(int currentStock, int minimumStock) => currentStock <= minimumStock;
}
