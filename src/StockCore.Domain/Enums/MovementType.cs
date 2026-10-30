namespace StockCore.Domain.Enums;

public enum MovementType
{
    /// <summary>Entrada de mercadoria (soma no saldo).</summary>
    Entry,

    /// <summary>Saída de mercadoria (subtrai do saldo).</summary>
    Exit,

    /// <summary>Estorno gerado pelo cancelamento de uma entrada ou saída.</summary>
    Reversal,
}
