namespace RepoSum.Application.Abstractions;

public interface IReadStateStore
{
    Task<bool> IsReadAsync(string summaryItemId, CancellationToken cancellationToken);
    Task MarkReadAsync(string summaryItemId, CancellationToken cancellationToken);
    Task MarkUnreadAsync(string summaryItemId, CancellationToken cancellationToken);
}
