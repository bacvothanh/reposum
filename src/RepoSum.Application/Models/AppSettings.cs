namespace RepoSum.Application.Models;

public sealed record AppSettings(
    Uri? OrganizationUri,
    string? ProjectName,
    string? PersonalAccessToken,
    IReadOnlyList<string> SelectedRepositoryIds
)
{
    public static AppSettings Empty { get; } = new(
        OrganizationUri: null,
        ProjectName: null,
        PersonalAccessToken: null,
        SelectedRepositoryIds: Array.Empty<string>());
}
