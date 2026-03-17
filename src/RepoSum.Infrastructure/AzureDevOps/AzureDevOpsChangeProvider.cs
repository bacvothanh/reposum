using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using RepoSum.Application.Abstractions;
using RepoSum.Domain.Models;
using RepoSum.Infrastructure.Caching;

namespace RepoSum.Infrastructure.AzureDevOps;

public sealed class AzureDevOpsChangeProvider(
    HttpClient httpClient,
    CacheProvider cache,
    ILogger<AzureDevOpsChangeProvider> logger) : IChangeProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly AsyncRetryPolicy<HttpResponseMessage> RetryPolicy = Policy
        .HandleResult<HttpResponseMessage>(r => (int)r.StatusCode >= 500 || r.StatusCode == (HttpStatusCode)429)
        .Or<HttpRequestException>()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: attempt => TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt)));

    public Task<IReadOnlyList<RepositoryRef>> GetRepositoriesAsync(
        Uri organizationUri,
        string projectName,
        string personalAccessToken,
        CancellationToken cancellationToken)
    {
        var key = $"azdo:repos:{organizationUri}:{projectName}";
        return cache.GetOrCreateAsync(
            key,
            ttl: TimeSpan.FromMinutes(10),
            factory: ct => GetRepositoriesUncachedAsync(organizationUri, projectName, personalAccessToken, ct),
            cancellationToken);
    }

    public Task<ChangeSet> GetChangesAsync(
        RepositoryRef repository,
        DateTimeOffset from,
        DateTimeOffset to,
        string personalAccessToken,
        CancellationToken cancellationToken)
    {
        var key = $"azdo:changes:{repository.OrganizationUri}:{repository.ProjectName}:{repository.Id}:{from.UtcDateTime:o}:{to.UtcDateTime:o}";
        return cache.GetOrCreateAsync(
            key,
            ttl: TimeSpan.FromMinutes(2),
            factory: ct => GetChangesUncachedAsync(repository, from, to, personalAccessToken, ct),
            cancellationToken);
    }

    private async Task<IReadOnlyList<RepositoryRef>> GetRepositoriesUncachedAsync(
        Uri organizationUri,
        string projectName,
        string personalAccessToken,
        CancellationToken cancellationToken)
    {
        var uri = new Uri($"{organizationUri.AbsoluteUri.TrimEnd('/')}/{Uri.EscapeDataString(projectName)}/_apis/git/repositories?api-version=7.1-preview.1");

        var resp = await SendWithRetryAsync(
            requestFactory: () =>
            {
                var req = new HttpRequestMessage(HttpMethod.Get, uri);
                AddPatAuth(req, personalAccessToken);
                return req;
            },
            cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("Azure DevOps repo list failed: {StatusCode}", (int)resp.StatusCode);
            return Array.Empty<RepositoryRef>();
        }

        var json = await resp.Content.ReadAsStringAsync(cancellationToken);
        var payload = JsonSerializer.Deserialize<RepoListResponse>(json, JsonOptions);

        var org = new Uri(organizationUri.AbsoluteUri.TrimEnd('/') + "/");

        return (payload?.Value ?? [])
            .Where(r => !string.IsNullOrWhiteSpace(r.Id) && !string.IsNullOrWhiteSpace(r.Name) && !string.IsNullOrWhiteSpace(r.WebUrl))
            .Select(r => new RepositoryRef(
                Id: r.Id!,
                Name: r.Name!,
                ProjectName: r.Project?.Name ?? projectName,
                OrganizationUri: org,
                WebUrl: new Uri(r.WebUrl!)))
            .OrderBy(r => r.Name)
            .ToList();
    }

    private async Task<ChangeSet> GetChangesUncachedAsync(
        RepositoryRef repository,
        DateTimeOffset from,
        DateTimeOffset to,
        string personalAccessToken,
        CancellationToken cancellationToken)
    {
        var commitsTask = GetCommitsAsync(repository, from, to, personalAccessToken, cancellationToken);
        var prsTask = GetPullRequestsAsync(repository, from, to, personalAccessToken, cancellationToken);
        var releasesTask = GetReleasesAsync(repository, from, to, personalAccessToken, cancellationToken);

        await Task.WhenAll(commitsTask, prsTask, releasesTask);

        return new ChangeSet(repository, commitsTask.Result, prsTask.Result, releasesTask.Result);
    }

    private async Task<IReadOnlyList<CommitInfo>> GetCommitsAsync(
        RepositoryRef repository,
        DateTimeOffset from,
        DateTimeOffset to,
        string personalAccessToken,
        CancellationToken cancellationToken)
    {
        var baseUri = repository.OrganizationUri.AbsoluteUri.TrimEnd('/');
        var project = Uri.EscapeDataString(repository.ProjectName);
        var repoId = Uri.EscapeDataString(repository.Id);

        var uri = new Uri($"{baseUri}/{project}/_apis/git/repositories/{repoId}/commits?searchCriteria.fromDate={Uri.EscapeDataString(from.UtcDateTime.ToString("o"))}&searchCriteria.toDate={Uri.EscapeDataString(to.UtcDateTime.ToString("o"))}&$top=100&api-version=7.1-preview.1");

        var resp = await SendWithRetryAsync(
            requestFactory: () =>
            {
                var req = new HttpRequestMessage(HttpMethod.Get, uri);
                AddPatAuth(req, personalAccessToken);
                return req;
            },
            cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            return Array.Empty<CommitInfo>();
        }

        var json = await resp.Content.ReadAsStringAsync(cancellationToken);
        var payload = JsonSerializer.Deserialize<CommitListResponse>(json, JsonOptions);

        return (payload?.Value ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c.CommitId) && !string.IsNullOrWhiteSpace(c.Comment))
            .Select(c =>
            {
                var authorName = c.Author?.Name ?? string.Empty;
                var authorDate = c.Author?.Date ?? DateTimeOffset.MinValue;
                var webUrl = BuildCommitWebUrl(repository, c.CommitId!);

                return new CommitInfo(
                    CommitId: c.CommitId!,
                    Message: c.Comment!,
                    Author: authorName,
                    Timestamp: authorDate,
                    WebUrl: webUrl);
            })
            .ToList();
    }

    private async Task<IReadOnlyList<PullRequestInfo>> GetPullRequestsAsync(
        RepositoryRef repository,
        DateTimeOffset from,
        DateTimeOffset to,
        string personalAccessToken,
        CancellationToken cancellationToken)
    {
        var baseUri = repository.OrganizationUri.AbsoluteUri.TrimEnd('/');
        var project = Uri.EscapeDataString(repository.ProjectName);
        var repoId = Uri.EscapeDataString(repository.Id);

        var uri = new Uri($"{baseUri}/{project}/_apis/git/repositories/{repoId}/pullrequests?searchCriteria.status=all&searchCriteria.minTime={Uri.EscapeDataString(from.UtcDateTime.ToString("o"))}&searchCriteria.maxTime={Uri.EscapeDataString(to.UtcDateTime.ToString("o"))}&$top=100&api-version=7.1-preview.1");

        var resp = await SendWithRetryAsync(
            requestFactory: () =>
            {
                var req = new HttpRequestMessage(HttpMethod.Get, uri);
                AddPatAuth(req, personalAccessToken);
                return req;
            },
            cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            return Array.Empty<PullRequestInfo>();
        }

        var json = await resp.Content.ReadAsStringAsync(cancellationToken);
        var payload = JsonSerializer.Deserialize<PullRequestListResponse>(json, JsonOptions);

        return (payload?.Value ?? [])
            .Select(pr => new PullRequestInfo(
                PullRequestId: pr.PullRequestId,
                Title: pr.Title ?? string.Empty,
                Description: pr.Description ?? string.Empty,
                Author: pr.CreatedBy?.DisplayName ?? string.Empty,
                CreatedDate: pr.CreationDate,
                WebUrl: pr.Links?.Web?.Href is { Length: > 0 } href ? new Uri(href) : BuildPullRequestWebUrl(repository, pr.PullRequestId),
                Status: pr.Status ?? string.Empty))
            .ToList();
    }

    private async Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(
        RepositoryRef repository,
        DateTimeOffset from,
        DateTimeOffset to,
        string personalAccessToken,
        CancellationToken cancellationToken)
    {
        var orgName = TryGetOrganizationName(repository.OrganizationUri);
        if (string.IsNullOrWhiteSpace(orgName))
        {
            return Array.Empty<ReleaseInfo>();
        }

        var project = Uri.EscapeDataString(repository.ProjectName);
        var uri = new Uri($"https://vsrm.dev.azure.com/{Uri.EscapeDataString(orgName)}/{project}/_apis/release/releases?minCreatedTime={Uri.EscapeDataString(from.UtcDateTime.ToString("o"))}&maxCreatedTime={Uri.EscapeDataString(to.UtcDateTime.ToString("o"))}&$top=50&api-version=7.1-preview.1");

        var resp = await SendWithRetryAsync(
            requestFactory: () =>
            {
                var req = new HttpRequestMessage(HttpMethod.Get, uri);
                AddPatAuth(req, personalAccessToken);
                return req;
            },
            cancellationToken);
        if (!resp.IsSuccessStatusCode)
        {
            return Array.Empty<ReleaseInfo>();
        }

        var json = await resp.Content.ReadAsStringAsync(cancellationToken);
        var payload = JsonSerializer.Deserialize<ReleaseListResponse>(json, JsonOptions);

        return (payload?.Value ?? [])
            .Select(r => new ReleaseInfo(
                ReleaseId: r.Id,
                Name: r.Name ?? string.Empty,
                Description: r.Description ?? string.Empty,
                CreatedOn: r.CreatedOn,
                WebUrl: r.Links?.Web?.Href is { Length: > 0 } href ? new Uri(href) : repository.WebUrl))
            .ToList();
    }

    private static void AddPatAuth(HttpRequestMessage request, string personalAccessToken)
    {
        // Azure DevOps PAT via Basic auth: username is empty, password is the PAT.
        var raw = ":" + (personalAccessToken ?? string.Empty);
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes(raw));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        return await RetryPolicy.ExecuteAsync(async ct =>
        {
            using var req = requestFactory();
            return await httpClient.SendAsync(req, ct);
        }, cancellationToken);
    }

    private static Uri BuildCommitWebUrl(RepositoryRef repository, string commitId)
    {
        var baseUrl = repository.WebUrl.AbsoluteUri.TrimEnd('/');
        return new Uri($"{baseUrl}/commit/{commitId}");
    }

    private static Uri BuildPullRequestWebUrl(RepositoryRef repository, int prId)
    {
        var baseUrl = repository.WebUrl.AbsoluteUri.TrimEnd('/');
        return new Uri($"{baseUrl}/pullrequest/{prId}");
    }

    private static string? TryGetOrganizationName(Uri organizationUri)
    {
        // Expected org URI forms:
        // - https://dev.azure.com/{org}
        // - https://{org}.visualstudio.com
        try
        {
            if (organizationUri.Host.Equals("dev.azure.com", StringComparison.OrdinalIgnoreCase))
            {
                var segments = organizationUri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
                return segments.Length >= 1 ? segments[0] : null;
            }

            if (organizationUri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
            {
                return organizationUri.Host.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private sealed class RepoListResponse
    {
        public List<RepoModel>? Value { get; set; }
    }

    private sealed class RepoModel
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? WebUrl { get; set; }
        public ProjectModel? Project { get; set; }
    }

    private sealed class ProjectModel
    {
        public string? Name { get; set; }
    }

    private sealed class CommitListResponse
    {
        public List<CommitModel>? Value { get; set; }
    }

    private sealed class CommitModel
    {
        public string? CommitId { get; set; }
        public string? Comment { get; set; }
        public CommitAuthorModel? Author { get; set; }
    }

    private sealed class CommitAuthorModel
    {
        public string? Name { get; set; }
        public DateTimeOffset Date { get; set; }
    }

    private sealed class PullRequestListResponse
    {
        public List<PullRequestModel>? Value { get; set; }
    }

    private sealed class PullRequestModel
    {
        public int PullRequestId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public IdentityModel? CreatedBy { get; set; }
        public DateTimeOffset CreationDate { get; set; }
        public string? Status { get; set; }
        public LinkContainer? Links { get; set; }
    }

    private sealed class IdentityModel
    {
        public string? DisplayName { get; set; }
    }

    private sealed class ReleaseListResponse
    {
        public List<ReleaseModel>? Value { get; set; }
    }

    private sealed class ReleaseModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public DateTimeOffset CreatedOn { get; set; }
        public LinkContainer? Links { get; set; }
    }

    private sealed class LinkContainer
    {
        public LinkModel? Web { get; set; }
    }

    private sealed class LinkModel
    {
        public string? Href { get; set; }
    }
}
