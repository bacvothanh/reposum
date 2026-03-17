using System.Text.Json;
using RepoSum.Application.Abstractions;

namespace RepoSum.Infrastructure.Storage;

public sealed class FileReadStateStore(AppDataPathProvider paths) : IReadStateStore
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<bool> IsReadAsync(string summaryItemId, CancellationToken cancellationToken)
    {
        var state = await LoadAsync(cancellationToken);
        return state.TryGetValue(summaryItemId, out var isRead) && isRead;
    }

    public async Task MarkReadAsync(string summaryItemId, CancellationToken cancellationToken)
    {
        var state = await LoadAsync(cancellationToken);
        state[summaryItemId] = true;
        await SaveAsync(state, cancellationToken);
    }

    public async Task MarkUnreadAsync(string summaryItemId, CancellationToken cancellationToken)
    {
        var state = await LoadAsync(cancellationToken);
        state[summaryItemId] = false;
        await SaveAsync(state, cancellationToken);
    }

    private async Task<Dictionary<string, bool>> LoadAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(paths.ReadStateFilePath))
            {
                return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            }

            var json = await File.ReadAllTextAsync(paths.ReadStateFilePath, cancellationToken);
            var state = JsonSerializer.Deserialize<Dictionary<string, bool>>(json, JsonOptions);
            return state ?? new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task SaveAsync(Dictionary<string, bool> state, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(paths.ReadStateFilePath)!);
            var json = JsonSerializer.Serialize(state, JsonOptions);
            await File.WriteAllTextAsync(paths.ReadStateFilePath, json, cancellationToken);
        }
        finally
        {
            Gate.Release();
        }
    }
}
