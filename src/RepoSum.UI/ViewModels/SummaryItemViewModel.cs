using CommunityToolkit.Mvvm.ComponentModel;
using RepoSum.Application.Models;
using RepoSum.Domain.Models;

namespace RepoSum.UI.ViewModels;

public sealed partial class SummaryItemViewModel : ObservableObject
{
    public SummaryItemViewModel(SummaryItemDto dto)
    {
        Item = dto.Item;
        _isRead = dto.IsRead;
    }

    public SummaryItem Item { get; }

    public string Id => Item.Id;
    public string RepositoryName => Item.Repository.Name;
    public string Title => Item.Title;
    public string Details => Item.Details;
    public string Author => string.IsNullOrWhiteSpace(Item.Author) ? "" : Item.Author;
    public DateTimeOffset Timestamp => Item.Timestamp;
    public ChangeCategory Category => Item.Category;
    public ChangeSource Source => Item.Source;
    public Uri WebUrl => Item.WebUrl;

    [ObservableProperty]
    private bool _isRead;
}
