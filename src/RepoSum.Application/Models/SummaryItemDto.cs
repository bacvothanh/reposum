using RepoSum.Domain.Models;

namespace RepoSum.Application.Models;

public sealed record SummaryItemDto(
    SummaryItem Item,
    bool IsRead
);
