using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace RepoSum.Infrastructure.Caching;

public sealed class CacheProvider(IMemoryCache memoryCache, string cacheDir)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken)
    {
        if (memoryCache.TryGetValue(key, out T? existing) && existing is not null)
        {
            return existing;
        }

        var filePath = GetFilePath(key);
        var now = DateTimeOffset.UtcNow;

        var fileEntry = await TryReadFileEntryAsync<T>(filePath, now, cancellationToken);
        if (fileEntry.found)
        {
            memoryCache.Set(key, fileEntry.value!, ttl);
            return fileEntry.value!;
        }

        var created = await factory(cancellationToken);
        memoryCache.Set(key, created!, ttl);
        await TryWriteFileEntryAsync(filePath, created, now.Add(ttl), cancellationToken);
        return created;
    }

    private string GetFilePath(string key)
    {
        Directory.CreateDirectory(cacheDir);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(cacheDir, hash + ".json");
    }

    private static async Task<(bool found, T? value)> TryReadFileEntryAsync<T>(string path, DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path))
            {
                return (false, default);
            }

            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var entry = JsonSerializer.Deserialize<FileCacheEntry<T>>(json, JsonOptions);

            if (entry is null || entry.ExpiresUtc <= now)
            {
                return (false, default);
            }

            return (true, entry.Value);
        }
        catch
        {
            return (false, default);
        }
    }

    private static async Task TryWriteFileEntryAsync<T>(string path, T value, DateTimeOffset expiresUtc, CancellationToken cancellationToken)
    {
        try
        {
            var entry = new FileCacheEntry<T> { ExpiresUtc = expiresUtc, Value = value };
            var json = JsonSerializer.Serialize(entry, JsonOptions);
            await File.WriteAllTextAsync(path, json, cancellationToken);
        }
        catch
        {
            // best-effort
        }
    }

    private sealed class FileCacheEntry<T>
    {
        public DateTimeOffset ExpiresUtc { get; set; }
        public T? Value { get; set; }
    }
}
