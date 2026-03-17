namespace RepoSum.Domain.Models;

public sealed record ReleaseInfo(
    int ReleaseId,
    string Name,
    string Description,
    DateTimeOffset CreatedOn,
    Uri WebUrl
);
