namespace RepoSum.Domain.Models;

public enum ChangeSource
{
    Commit,
    PullRequest,
    Release,
}

public enum ChangeCategory
{
    Feature,
    BugFix,
    BreakingChange,
    Other,
}

public sealed record SummaryItem(
    string Id,
    RepositoryRef Repository,
    ChangeSource Source,
    ChangeCategory Category,
    string Title,
    string Details,
    string Author,
    DateTimeOffset Timestamp,
    Uri WebUrl
);
