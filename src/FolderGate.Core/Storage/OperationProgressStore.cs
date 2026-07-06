using System.Text.Json;

namespace FolderGate.Core.Storage;

public sealed class OperationProgressStore
{
    private readonly AppPaths _paths;
    private readonly JsonSerializerOptions _jsonOptions = JsonOptionsFactory.Create();

    public OperationProgressStore(AppPaths paths)
    {
        _paths = paths;
    }

    public string ProgressDirectory
    {
        get
        {
            string directory = Path.Combine(_paths.LogDirectory, "progress");
            Directory.CreateDirectory(directory);
            return directory;
        }
    }

    public string GetProgressPath(string operationId)
    {
        return Path.Combine(ProgressDirectory, $"progress-{Sanitize(operationId)}.json");
    }

    public string GetCancelPath(string operationId)
    {
        return Path.Combine(ProgressDirectory, $"cancel-{Sanitize(operationId)}.flag");
    }

    public void SaveProgress(OperationProgressSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        string path = GetProgressPath(snapshot.OperationId);
        string tempPath = path + ".tmp";
        string json = JsonSerializer.Serialize(snapshot, _jsonOptions);
        File.WriteAllText(tempPath, json);

        if (File.Exists(path))
        {
            File.Replace(tempPath, path, null);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }

    public OperationProgressSnapshot? TryLoadProgress(string operationId)
    {
        string path = GetProgressPath(operationId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<OperationProgressSnapshot>(json, _jsonOptions);
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public void RequestCancel(string operationId)
    {
        File.WriteAllText(GetCancelPath(operationId), DateTimeOffset.UtcNow.ToString("O"));
    }

    public bool IsCancellationRequested(string operationId)
    {
        return File.Exists(GetCancelPath(operationId));
    }

    public void ClearCancel(string operationId)
    {
        string path = GetCancelPath(operationId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string Sanitize(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}
