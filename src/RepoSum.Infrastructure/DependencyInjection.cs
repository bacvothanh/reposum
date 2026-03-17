using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using RepoSum.Application.Abstractions;
using RepoSum.Infrastructure.AzureDevOps;
using RepoSum.Infrastructure.Caching;
using RepoSum.Infrastructure.Storage;

namespace RepoSum.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddRepoSumInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<AppDataPathProvider>();
        services.AddSingleton<DpapiProtector>();

        services.AddSingleton<ISettingsService, FileSettingsService>();
        services.AddSingleton<IReadStateStore, FileReadStateStore>();

        services.AddMemoryCache();

        services.AddSingleton<CacheProvider>(sp =>
        {
            var paths = sp.GetRequiredService<AppDataPathProvider>();
            Directory.CreateDirectory(paths.CacheDir);
            return new CacheProvider(sp.GetRequiredService<IMemoryCache>(), paths.CacheDir);
        });

        services.AddHttpClient<AzureDevOpsChangeProvider>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<IChangeProvider>(sp => sp.GetRequiredService<AzureDevOpsChangeProvider>());

        return services;
    }
}
