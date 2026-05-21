using Microsoft.Extensions.Logging;
using RepoSum.Application.Abstractions;
using RepoSum.Application.Models;
using RepoSum.Domain.Models;

namespace RepoSum.Application.Services;

public sealed class ChangeSummaryService(
    ISettingsService settingsService,
    IChangeProvider changeProvider,
    IAiSummarizer aiSummarizer,
    IReadStateStore readStateStore,
    ILogger<ChangeSummaryService> logger) : IChangeSummaryService
{
    public async Task<IReadOnlyList<SummaryItemDto>> GetSummaryAsync(ChangeSummaryQuery query, CancellationToken cancellationToken)
    {
        var settings = await settingsService.GetAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(settings.PersonalAccessToken))
        {
            return Array.Empty<SummaryItemDto>();
        }

        var selectedRepos = query.Repositories;
        if (selectedRepos.Count == 0)
        {
            return Array.Empty<SummaryItemDto>();
        }

        var allItems = new List<SummaryItem>();

        foreach (var repo in selectedRepos)
        {
            try
            {
                var changes = await changeProvider.GetChangesAsync(repo, query.From, query.To, settings.PersonalAccessToken!, cancellationToken);
                var summarized = await aiSummarizer.SummarizeAsync(changes, cancellationToken);
                allItems.AddRange(summarized);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch changes for repository {RepoId} ({RepoName})", repo.Id, repo.Name);
            }
        }

        var filtered = ApplyFilters(allItems, query);

        var result = new List<SummaryItemDto>(filtered.Count);
        foreach (var item in filtered)
        {
            var isRead = await readStateStore.IsReadAsync(item.Id, cancellationToken);
            result.Add(new SummaryItemDto(item, isRead));
        }

        return result;
    }

    private static List<SummaryItem> ApplyFilters(List<SummaryItem> items, ChangeSummaryQuery query)
    {
        var result = items
            .Where(i => i.Timestamp >= query.From && i.Timestamp <= query.To)
            .ToList();

        if (!string.IsNullOrWhiteSpace(query.AuthorFilter))
        {
            result = result
                .Where(i => i.Author.Contains(query.AuthorFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            result = result
                .Where(i => (i.Title + "\n" + i.Details).Contains(query.SearchText, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return result
            .OrderByDescending(i => i.Timestamp)
            .ToList();
    }
}
