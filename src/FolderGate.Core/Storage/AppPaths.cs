namespace FolderGate.Core.Storage;

public sealed record AppPaths(
    string ProjectRoot,
    string DataDirectory,
    string ConfigDirectory,
    string LogDirectory,
    string BackupDirectory,
    string ReleaseDirectory,
    string ConfigFilePath,
    string LogFilePath)
{
    public static AppPaths Resolve(string? explicitProjectRoot = null)
    {
        string projectRoot = string.IsNullOrWhiteSpace(explicitProjectRoot)
            ? DiscoverProjectRoot()
            : Path.GetFullPath(explicitProjectRoot);

        string data = Path.Combine(projectRoot, "data");
        string configs = Path.Combine(data, "configs");
        string logs = Path.Combine(data, "logs");
        string backups = Path.Combine(data, "backups");
        string release = Path.Combine(projectRoot, "release");

        Directory.CreateDirectory(configs);
        Directory.CreateDirectory(logs);
        Directory.CreateDirectory(backups);
        Directory.CreateDirectory(release);

        return new AppPaths(
            projectRoot,
            data,
            configs,
            logs,
            backups,
            release,
            Path.Combine(configs, "foldergate.config.json"),
            Path.Combine(logs, "foldergate.jsonl"));
    }

    private static string DiscoverProjectRoot()
    {
        string[] candidates =
        [
            Directory.GetCurrentDirectory(),
            AppContext.BaseDirectory
        ];

        foreach (string candidate in candidates)
        {
            string? current = Path.GetFullPath(candidate);
            while (!string.IsNullOrWhiteSpace(current))
            {
                if (File.Exists(Path.Combine(current, "FolderGate.sln")) ||
                    (Directory.Exists(Path.Combine(current, "data", "configs")) &&
                     Directory.Exists(Path.Combine(current, "data", "backups"))))
                {
                    return current;
                }

                current = Directory.GetParent(current)?.FullName;
            }
        }

        return Path.GetFullPath(Directory.GetCurrentDirectory());
    }
}
