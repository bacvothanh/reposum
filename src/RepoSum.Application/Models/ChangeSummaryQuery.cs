using RepoSum.Domain.Models;

namespace RepoSum.Application.Models;

public sealed record ChangeSummaryQuery(
    DateTimeOffset From,
    DateTimeOffset To,
    IReadOnlyList<RepositoryRef> Repositories,
    string? AuthorFilter,
    string? SearchText
);
