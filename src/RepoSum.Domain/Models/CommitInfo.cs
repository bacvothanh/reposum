namespace RepoSum.Domain.Models;

public sealed record CommitInfo(
    string CommitId,
    string Message,
    string Author,
    DateTimeOffset Timestamp,
    Uri WebUrl
);
