using RepoSum.Domain.Models;

namespace RepoSum.Application.Models;

public sealed record PersistedRepository(
    string Id,
    string Name,
    string ProjectName,
    Uri OrganizationUri,
    Uri WebUrl)
{
    public RepositoryRef ToRepositoryRef() => new(
        Id: Id,
        Name: Name,
        ProjectName: ProjectName,
        OrganizationUri: OrganizationUri,
        WebUrl: WebUrl);

    public static PersistedRepository FromRepositoryRef(RepositoryRef repository) => new(
        Id: repository.Id,
        Name: repository.Name,
        ProjectName: repository.ProjectName,
        OrganizationUri: repository.OrganizationUri,
        WebUrl: repository.WebUrl);
}

public sealed record AppSettings(
    Uri? OrganizationUri,
    string? ProjectName,
    string? PersonalAccessToken,
    IReadOnlyList<string> SelectedRepositoryIds,
    IReadOnlyList<PersistedRepository> SelectedRepositories)
{
    public static AppSettings Empty { get; } = new(
        OrganizationUri: null,
        ProjectName: null,
        PersonalAccessToken: null,
        SelectedRepositoryIds: Array.Empty<string>(),
        SelectedRepositories: Array.Empty<PersistedRepository>());
}
