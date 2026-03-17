using RepoSum.Domain.Models;

namespace RepoSum.Application.Abstractions;

public interface IChangeProvider
{
    Task<IReadOnlyList<RepositoryRef>> GetRepositoriesAsync(
        Uri organizationUri,
        string projectName,
        string personalAccessToken,
        CancellationToken cancellationToken);

    Task<ChangeSet> GetChangesAsync(
        RepositoryRef repository,
        DateTimeOffset from,
        DateTimeOffset to,
        string personalAccessToken,
        CancellationToken cancellationToken);
}
