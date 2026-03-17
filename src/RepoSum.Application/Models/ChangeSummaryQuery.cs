namespace RepoSum.Application.Models;

public sealed record ChangeSummaryQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<string> RepositoryIds,
    string? AuthorFilter,
    string? SearchText
);
