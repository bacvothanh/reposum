namespace RepoSum.Infrastructure.Storage;

public sealed class AppDataPathProvider
{
    private readonly string _baseDir;

    public AppDataPathProvider(string appName = "RepoSum")
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _baseDir = Path.Combine(root, appName);
        Directory.CreateDirectory(_baseDir);
    }

    public string BaseDir => _baseDir;

    public string SettingsFilePath => Path.Combine(_baseDir, "settings.json");

    public string ReadStateFilePath => Path.Combine(_baseDir, "readstate.json");

    public string CacheDir => Path.Combine(_baseDir, "cache");
}
