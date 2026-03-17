using RepoSum.Application.Models;

namespace RepoSum.Application.Abstractions;

public interface IChangeSummaryService
{
    Task<IReadOnlyList<SummaryItemDto>> GetSummaryAsync(ChangeSummaryQuery query, CancellationToken cancellationToken);
}
