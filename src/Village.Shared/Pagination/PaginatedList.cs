using Microsoft.EntityFrameworkCore;

namespace Village.Shared.Pagination;

public class PaginatedList<T>
{
    public List<T> Items { get; init; } = [];
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }
}

public static class PaginationExtensions
{
    /// <summary>
    /// Returns a cursor-paginated list. Currently uses limit/offset internally
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
