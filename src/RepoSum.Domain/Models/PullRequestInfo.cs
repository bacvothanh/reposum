namespace RepoSum.Domain.Models;

public sealed record PullRequestInfo(
    int PullRequestId,
    string Title,
    string Description,
    string Author,
    DateTimeOffset CreatedDate,
    Uri WebUrl,
    string Status
);
