using System.Text.Json;
using FolderGate.Core.Models;

namespace FolderGate.Core.Storage;

public sealed class ConfigStore
{
    private readonly AppPaths _paths;
    private readonly JsonSerializerOptions _jsonOptions = JsonOptionsFactory.Create();

    public ConfigStore(AppPaths paths)
    {
        _paths = paths;
    }

    public FolderGateConfig Load()
    {
        if (!File.Exists(_paths.ConfigFilePath))
        {
            return new FolderGateConfig();
        }

        string json = File.ReadAllText(_paths.ConfigFilePath);
        FolderGateConfig? config = JsonSerializer.Deserialize<FolderGateConfig>(json, _jsonOptions);
        return config ?? new FolderGateConfig();
    }

    public void Save(FolderGateConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        config.UpdatedUtc = DateTimeOffset.UtcNow;
        Directory.CreateDirectory(_paths.ConfigDirectory);

        string tempPath = _paths.ConfigFilePath + ".tmp";
        string json = JsonSerializer.Serialize(config, _jsonOptions);
        File.WriteAllText(tempPath, json);

        if (File.Exists(_paths.ConfigFilePath))
        {
            File.Replace(tempPath, _paths.ConfigFilePath, null);
        }
        else
        {
            File.Move(tempPath, _paths.ConfigFilePath);
        }
    }

    public RegisteredFolder? FindFolder(string targetId)
    {
        return Load().Folders.FirstOrDefault(folder => string.Equals(folder.Id, targetId, StringComparison.OrdinalIgnoreCase));
    }
}
