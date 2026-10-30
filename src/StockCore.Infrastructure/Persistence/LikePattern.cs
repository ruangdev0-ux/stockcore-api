namespace StockCore.Infrastructure.Persistence;

internal static class LikePattern
{
    /// <summary>Monta "%termo%" escapando os curingas do LIKE (%, _ e \).</summary>
    public static string Contains(string term) =>
        "%" + term.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
}
