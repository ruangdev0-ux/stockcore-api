using StockCore.Application.Common;

namespace StockCore.Infrastructure.Persistence;

internal static class QueryableExtensions
{
    public static IQueryable<T> Page<T>(this IQueryable<T> query, PageQuery page) =>
        query.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize);
}
