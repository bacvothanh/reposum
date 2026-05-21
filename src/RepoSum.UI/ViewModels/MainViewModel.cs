using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using RepoSum.Application.Abstractions;
using RepoSum.Application.Models;
using RepoSum.Domain.Models;

namespace RepoSum.UI.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IChangeProvider _changeProvider;
    private readonly IChangeSummaryService _changeSummaryService;
    private readonly IReadStateStore _readStateStore;
    private readonly ILogger<MainViewModel> _logger;
    private readonly HashSet<string> _removedSelectedRepositoryIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RepoSelectionItemViewModel> _preservedSelectedRepositories = new(StringComparer.OrdinalIgnoreCase);

    public MainViewModel(
        ISettingsService settingsService,
        IChangeProvider changeProvider,
        IChangeSummaryService changeSummaryService,
        IReadStateStore readStateStore,
        ILogger<MainViewModel> logger)
    {
        _settingsService = settingsService;
        _changeProvider = changeProvider;
        _changeSummaryService = changeSummaryService;
        _readStateStore = readStateStore;
        _logger = logger;

        DateRangePresets = new[]
        {
            DateRangePreset.Last24Hours,
            DateRangePreset.Last7Days,
            DateRangePreset.Last30Days,
        };

        SelectedDateRangePreset = DateRangePreset.Last7Days;

        Tabs.Add(new DashboardTabViewModel("All", repositoryId: null));

        _ = InitializeAsync();
    }

    public ObservableCollection<RepoSelectionItemViewModel> Repositories { get; } = new();

    public ObservableCollection<DashboardTabViewModel> Tabs { get; } = new();

    public DateRangePreset[] DateRangePresets { get; }

    [ObservableProperty]
    private string _organizationUriText = string.Empty;

    [ObservableProperty]
    private string _projectName = string.Empty;

    [ObservableProperty]
    private string _personalAccessToken = string.Empty;

    [ObservableProperty]
    private DateRangePreset _selectedDateRangePreset;

    [ObservableProperty]
    private string _authorFilter = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = string.Empty;

    private async Task InitializeAsync()
    {
        try
        {
            var settings = await _settingsService.GetAsync(CancellationToken.None);

            OrganizationUriText = settings.OrganizationUri?.ToString() ?? string.Empty;
            ProjectName = settings.ProjectName ?? string.Empty;
            PersonalAccessToken = settings.PersonalAccessToken ?? string.Empty;

            await LoadRepositoriesInternalAsync(preselectFromSettings: true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize settings");
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        if (!TryBuildOrganizationUri(out var orgUri))
        {
            StatusText = "Invalid Organization URL.";
            return;
        }

        var selected = Repositories.Where(r => r.IsSelected).Select(r => r.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        selected.UnionWith(_removedSelectedRepositoryIds);
        selected.UnionWith(_preservedSelectedRepositories.Values.Where(r => r.IsSelected).Select(r => r.Id));

        var settings = new AppSettings(
            OrganizationUri: orgUri,
            ProjectName: string.IsNullOrWhiteSpace(ProjectName) ? null : ProjectName.Trim(),
            PersonalAccessToken: string.IsNullOrWhiteSpace(PersonalAccessToken) ? null : PersonalAccessToken,
            SelectedRepositoryIds: selected.ToList());

        await _settingsService.SaveAsync(settings, CancellationToken.None);
        StatusText = "Settings saved.";

        RebuildTabsFromSelection();
    }

    [RelayCommand]
    private async Task LoadRepositoriesAsync()
        => await LoadRepositoriesInternalAsync(preselectFromSettings: false, CancellationToken.None);

    private async Task LoadRepositoriesInternalAsync(bool preselectFromSettings, CancellationToken cancellationToken)
    {
        if (!TryBuildOrganizationUri(out var orgUri))
        {
            StatusText = "Enter a valid Azure DevOps Organization URL.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            StatusText = "Enter a Project name.";
            return;
        }

        if (string.IsNullOrWhiteSpace(PersonalAccessToken))
        {
            StatusText = "Enter a PAT (Personal Access Token).";
            return;
        }

        IsBusy = true;
        StatusText = "Loading repositories...";

        try
        {
            var existingSelected = preselectFromSettings
                ? (await _settingsService.GetAsync(cancellationToken)).SelectedRepositoryIds.ToHashSet(StringComparer.OrdinalIgnoreCase)
                : Repositories.Where(r => r.IsSelected).Select(r => r.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            existingSelected.UnionWith(_removedSelectedRepositoryIds);
            existingSelected.UnionWith(_preservedSelectedRepositories.Values.Where(r => r.IsSelected).Select(r => r.Id));

            foreach (var repository in Repositories)
            {
                if (repository.IsSelected || _preservedSelectedRepositories.ContainsKey(repository.Id))
                {
                    _preservedSelectedRepositories[repository.Id] = repository;
                }
            }

            var repos = await _changeProvider.GetRepositoriesAsync(orgUri, ProjectName.Trim(), PersonalAccessToken, cancellationToken);
            var fetchedRepositoryIds = repos.Select(r => r.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            Repositories.Clear();

            foreach (var repo in repos.OrderBy(r => r.Name))
            {
                var vm = new RepoSelectionItemViewModel(repo)
                {
                    IsSelected = existingSelected.Contains(repo.Id),
                };

                Repositories.Add(vm);
                _preservedSelectedRepositories[repo.Id] = vm;
            }

            var missingSelectedRepositories = _preservedSelectedRepositories.Values
                .Where(r => existingSelected.Contains(r.Id))
                .Where(r => !fetchedRepositoryIds.Contains(r.Id))
                .OrderBy(r => r.Name)
                .ToList();

            foreach (var repository in missingSelectedRepositories)
            {
                repository.IsSelected = true;
                Repositories.Add(repository);
            }

            _removedSelectedRepositoryIds.Clear();
            StatusText = $"Loaded {repos.Count} repositories.";
            RebuildTabsFromSelection();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load repositories");
            StatusText = "Failed to load repositories. Check PAT/org/project.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var selectedRepositories = Repositories.Where(r => r.IsSelected).Select(r => r.Repository).ToList();
        if (selectedRepositories.Count == 0)
        {
            StatusText = "Select at least one repository.";
            return;
        }

        var (from, to) = GetDateRange(SelectedDateRangePreset);

        IsBusy = true;
        StatusText = "Fetching changes...";

        try
        {
            var query = new ChangeSummaryQuery(
                From: from,
                To: to,
                Repositories: selectedRepositories,
                AuthorFilter: string.IsNullOrWhiteSpace(AuthorFilter) ? null : AuthorFilter.Trim(),
                SearchText: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim());

            var dtos = await _changeSummaryService.GetSummaryAsync(query, CancellationToken.None);
            var items = dtos.Select(d => new SummaryItemViewModel(d)).ToList();

            PopulateTabs(items);

            StatusText = $"{items.Count} items.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refresh failed");
            StatusText = "Refresh failed.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void RemoveSelectedRepositories()
    {
        var selectedRepositories = Repositories.Where(r => r.IsSelected).ToList();
        if (selectedRepositories.Count == 0)
        {
            StatusText = "No selected repositories to remove.";
            return;
        }

        foreach (var repository in selectedRepositories)
        {
            _removedSelectedRepositoryIds.Add(repository.Id);
            _preservedSelectedRepositories[repository.Id] = repository;
            Repositories.Remove(repository);
        }

        RebuildTabsFromSelection();
        StatusText = $"Removed {selectedRepositories.Count} selected repositories.";
    }

    [RelayCommand]
    private async Task ToggleReadAsync(SummaryItemViewModel item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            if (item.IsRead)
            {
                await _readStateStore.MarkUnreadAsync(item.Id, CancellationToken.None);
                item.IsRead = false;
            }
            else
            {
                await _readStateStore.MarkReadAsync(item.Id, CancellationToken.None);
                item.IsRead = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to toggle read state");
        }
    }

    [RelayCommand]
    private void OpenWebUrl(SummaryItemViewModel item)
    {
        if (item?.WebUrl is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = item.WebUrl.ToString(),
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open web URL");
            StatusText = "Failed to open browser.";
        }
    }

    private void RebuildTabsFromSelection()
    {
        var selected = Repositories.Where(r => r.IsSelected).Select(r => r.Repository).ToList();

        Tabs.Clear();
        Tabs.Add(new DashboardTabViewModel("All", repositoryId: null));
        foreach (var repo in selected.OrderBy(r => r.Name))
        {
            Tabs.Add(new DashboardTabViewModel(repo.Name, repo.Id));
        }
    }

    private void PopulateTabs(List<SummaryItemViewModel> items)
    {
        foreach (var tab in Tabs)
        {
            tab.Items.Clear();

            IEnumerable<SummaryItemViewModel> filtered = items;
            if (!string.IsNullOrWhiteSpace(tab.RepositoryId))
            {
                filtered = filtered
                    .Where(i => i.Item.Repository.Id.Equals(tab.RepositoryId, StringComparison.OrdinalIgnoreCase))
                    .Where(i => i.Source != ChangeSource.Release);
            }

            foreach (var item in filtered.OrderByDescending(i => i.Timestamp))
            {
                tab.Items.Add(item);
            }
        }
    }

    private (DateTimeOffset from, DateTimeOffset to) GetDateRange(DateRangePreset preset)
    {
        var to = DateTimeOffset.UtcNow;
        var from = preset switch
        {
            DateRangePreset.Last24Hours => to.AddHours(-24),
            DateRangePreset.Last7Days => to.AddDays(-7),
            DateRangePreset.Last30Days => to.AddDays(-30),
            _ => to.AddDays(-7),
        };

        return (from, to);
    }

    private bool TryBuildOrganizationUri(out Uri organizationUri)
    {
        organizationUri = null!;

        if (string.IsNullOrWhiteSpace(OrganizationUriText))
        {
            return false;
        }

        if (!Uri.TryCreate(OrganizationUriText.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        organizationUri = uri;
        return true;
    }
}
