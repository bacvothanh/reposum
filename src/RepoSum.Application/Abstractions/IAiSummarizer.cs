using RepoSum.Domain.Models;

namespace RepoSum.Application.Abstractions;

public interface IAiSummarizer
{
    Task<IReadOnlyList<SummaryItem>> SummarizeAsync(ChangeSet changes, CancellationToken cancellationToken);
}
