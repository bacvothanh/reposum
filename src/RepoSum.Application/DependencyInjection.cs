using Microsoft.Extensions.DependencyInjection;
using RepoSum.Application.Abstractions;
using RepoSum.Application.Services;

namespace RepoSum.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddRepoSumApplication(this IServiceCollection services)
    {
        services.AddSingleton<IAiSummarizer, HeuristicAiSummarizer>();
        services.AddSingleton<IChangeSummaryService, ChangeSummaryService>();
        return services;
    }
}
