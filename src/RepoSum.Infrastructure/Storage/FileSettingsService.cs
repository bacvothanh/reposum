using System.Text.Json;
using RepoSum.Application.Abstractions;
using RepoSum.Application.Models;

namespace RepoSum.Infrastructure.Storage;

public sealed class FileSettingsService(AppDataPathProvider paths, DpapiProtector protector) : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(paths.SettingsFilePath))
        {
            return AppSettings.Empty;
        }

        try
        {
            var json = await File.ReadAllTextAsync(paths.SettingsFilePath, cancellationToken);
            var model = JsonSerializer.Deserialize<SettingsFileModel>(json, JsonOptions);
            if (model is null)
            {
                return AppSettings.Empty;
            }

            return new AppSettings(
                OrganizationUri: string.IsNullOrWhiteSpace(model.OrganizationUri) ? null : new Uri(model.OrganizationUri),
                ProjectName: model.ProjectName,
                PersonalAccessToken: protector.UnprotectFromBase64(model.PatProtectedBase64 ?? string.Empty),
                SelectedRepositoryIds: model.SelectedRepositoryIds ?? Array.Empty<string>());
        }
        catch
        {
            return AppSettings.Empty;
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        var model = new SettingsFileModel
        {
            OrganizationUri = settings.OrganizationUri?.ToString(),
            ProjectName = settings.ProjectName,
            PatProtectedBase64 = protector.ProtectToBase64(settings.PersonalAccessToken ?? string.Empty),
            SelectedRepositoryIds = settings.SelectedRepositoryIds.ToArray(),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(paths.SettingsFilePath)!);
        var json = JsonSerializer.Serialize(model, JsonOptions);
        await File.WriteAllTextAsync(paths.SettingsFilePath, json, cancellationToken);
    }

    private sealed class SettingsFileModel
    {
        public string? OrganizationUri { get; set; }
        public string? ProjectName { get; set; }
        public string? PatProtectedBase64 { get; set; }
        public string[]? SelectedRepositoryIds { get; set; }
    }
}
