using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RepoSum.UI.ViewModels;

public sealed partial class DashboardTabViewModel : ObservableObject
{
    public DashboardTabViewModel(string title, string? repositoryId)
    {
        Title = title;
        RepositoryId = repositoryId;
    }

    public string Title { get; }

    public string? RepositoryId { get; }

    public ObservableCollection<SummaryItemViewModel> Items { get; } = new();
}
