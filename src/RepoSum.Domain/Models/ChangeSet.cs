namespace RepoSum.Domain.Models;

public sealed record ChangeSet(
    RepositoryRef Repository,
    IReadOnlyList<CommitInfo> Commits,
    IReadOnlyList<PullRequestInfo> PullRequests,
    IReadOnlyList<ReleaseInfo> Releases
);
