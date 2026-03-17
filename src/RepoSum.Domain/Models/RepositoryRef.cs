namespace RepoSum.Domain.Models;

public sealed record RepositoryRef(
    string Id,
    string Name,
    string ProjectName,
    Uri OrganizationUri,
    Uri WebUrl
);
