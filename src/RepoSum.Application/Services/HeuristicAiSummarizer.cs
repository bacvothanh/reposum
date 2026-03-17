using System.Text.RegularExpressions;
using RepoSum.Application.Abstractions;
using RepoSum.Domain.Models;

namespace RepoSum.Application.Services;

public sealed class HeuristicAiSummarizer : IAiSummarizer
{
    private static readonly Regex BreakingRegex = new("\\bBREAKING CHANGE\\b|\\bbreaking\\b|!:", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FeatureRegex = new("\\bfeat\\b|\\bfeature\\b|\\badd\\b|\\bimplement\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex FixRegex = new("\\bfix\\b|\\bbug\\b|\\bhotfix\\b|\\bpatch\\b|\\bresolve\\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task<IReadOnlyList<SummaryItem>> SummarizeAsync(ChangeSet changes, CancellationToken cancellationToken)
    {
        var items = new List<SummaryItem>();

        foreach (var commit in changes.Commits)
        {
            items.Add(new SummaryItem(
                Id: $"commit:{changes.Repository.Id}:{commit.CommitId}",
                Repository: changes.Repository,
                Source: ChangeSource.Commit,
                Category: Categorize(commit.Message),
                Title: FirstLine(commit.Message),
                Details: commit.Message.Trim(),
                Author: commit.Author,
                Timestamp: commit.Timestamp,
                WebUrl: commit.WebUrl));
        }

        foreach (var pr in changes.PullRequests)
        {
            var combined = (pr.Title + "\n" + pr.Description).Trim();
            items.Add(new SummaryItem(
                Id: $"pr:{changes.Repository.Id}:{pr.PullRequestId}",
                Repository: changes.Repository,
                Source: ChangeSource.PullRequest,
                Category: Categorize(combined),
                Title: pr.Title.Trim(),
                Details: combined,
                Author: pr.Author,
                Timestamp: pr.CreatedDate,
                WebUrl: pr.WebUrl));
        }

        foreach (var release in changes.Releases)
        {
            var combined = (release.Name + "\n" + release.Description).Trim();
            items.Add(new SummaryItem(
                Id: $"release:{changes.Repository.Id}:{release.ReleaseId}",
                Repository: changes.Repository,
                Source: ChangeSource.Release,
                Category: Categorize(combined),
                Title: release.Name.Trim(),
                Details: combined,
                Author: string.Empty,
                Timestamp: release.CreatedOn,
                WebUrl: release.WebUrl));
        }

        return Task.FromResult<IReadOnlyList<SummaryItem>>(items
            .OrderByDescending(i => i.Timestamp)
            .ToList());
    }

    private static ChangeCategory Categorize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return ChangeCategory.Other;
        }

        if (BreakingRegex.IsMatch(text))
        {
            return ChangeCategory.BreakingChange;
        }

        if (FixRegex.IsMatch(text))
        {
            return ChangeCategory.BugFix;
        }

        if (FeatureRegex.IsMatch(text))
        {
            return ChangeCategory.Feature;
        }

        return ChangeCategory.Other;
    }

    private static string FirstLine(string text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        var idx = trimmed.IndexOfAny(['\r', '\n']);
        return idx >= 0 ? trimmed[..idx] : trimmed;
    }
}
