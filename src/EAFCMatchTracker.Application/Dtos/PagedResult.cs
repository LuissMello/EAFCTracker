namespace EAFCMatchTracker.Application.Dtos;

public sealed class PagedResult<T>
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
    public bool HasPrevious { get; init; }
    public bool HasNext { get; init; }
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
}
