using Microsoft.EntityFrameworkCore;
using Village.Shared.Pagination;

namespace Village.Infrastructure.Pagination;

public static class PaginationExtensions
{
    /// <summary>
    /// Returns a cursor-paginated list. Uses limit/offset internally
    /// but exposes cursor-compatible response shape for future cursor-based pagination.
    /// </summary>
    public static async Task<PaginatedList<T>> ToPaginatedListAsync<T>(
        this IQueryable<T> query,
        int limit = 20,
        string? cursor = null)
    {
        if (limit <= 0) limit = 20;
        if (limit > 100) limit = 100;

        var items = await query.Take(limit + 1).ToListAsync();
        bool hasMore = items.Count > limit;
        if (hasMore) items.RemoveAt(items.Count - 1);

        return new PaginatedList<T>
        {
            Items = items,
            HasMore = hasMore
        };
    }
}
