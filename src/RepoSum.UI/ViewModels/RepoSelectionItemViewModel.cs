using CommunityToolkit.Mvvm.ComponentModel;
using RepoSum.Domain.Models;

namespace RepoSum.UI.ViewModels;

public sealed partial class RepoSelectionItemViewModel : ObservableObject
{
    public RepoSelectionItemViewModel(RepositoryRef repository)
    {
        Repository = repository;
        _isSelected = false;
    }

    public RepositoryRef Repository { get; }

    public string Id => Repository.Id;

    public string Name => Repository.Name;

    [ObservableProperty]
    private bool _isSelected;
}
