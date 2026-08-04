namespace Village.Shared.Pagination;

public class PaginatedList<T>
{
    public List<T> Items { get; init; } = [];
    public string? NextCursor { get; init; }
    public bool HasMore { get; init; }
}
