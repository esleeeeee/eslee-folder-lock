using System.Globalization;
using System.Text.Json;
using FolderGate.Core.Localization;
using FolderGate.Core.Storage;

namespace FolderGate.Core.Acl;

public sealed class AclBackupStore
{
    private readonly AppPaths _paths;
    private readonly JsonSerializerOptions _jsonOptions = JsonOptionsFactory.Create();

    public AclBackupStore(AppPaths paths)
    {
        _paths = paths;
    }

    public string CreateBackupPath(string targetId, string operationId)
    {
        string targetDirectory = Path.Combine(_paths.BackupDirectory, SanitizeSegment(targetId));
        Directory.CreateDirectory(targetDirectory);
        return Path.Combine(targetDirectory, operationId + ".json");
    }

    public void Save(string path, AclBackupFile backup)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path) ?? _paths.BackupDirectory);
        string json = JsonSerializer.Serialize(backup, _jsonOptions);
        File.WriteAllText(path, json);
    }

    public AclBackupFile Load(string path)
    {
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AclBackupFile>(json, _jsonOptions)
            ?? throw new InvalidDataException(AppText.AclBackupReadFailed);
    }

    public IReadOnlyList<string> ListBackups(string targetId)
    {
        string targetDirectory = Path.Combine(_paths.BackupDirectory, SanitizeSegment(targetId));
        if (!Directory.Exists(targetDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(targetDirectory, "*.json")
            .Select(path => new BackupListItem(path, ReadBackupCreatedUtcOrFileTimeUtc(path)))
            .OrderByDescending(item => item.CreatedUtc)
            .ThenByDescending(item => File.GetLastWriteTimeUtc(item.Path))
            .Select(item => item.Path)
            .ToList();
    }

    private static DateTimeOffset ReadBackupCreatedUtcOrFileTimeUtc(string path)
    {
        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (TryReadUtcProperty(document.RootElement, "CreatedUtc", out DateTimeOffset createdUtc) ||
                TryReadUtcProperty(document.RootElement, "TimestampUtc", out createdUtc))
            {
                return createdUtc;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }

        return new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
    }

    private static bool TryReadUtcProperty(JsonElement element, string propertyName, out DateTimeOffset value)
    {
        value = default;
        if (!element.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        string? text = property.GetString();
        if (!DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
        {
            return false;
        }

        value = parsed.ToUniversalTime();
        return true;
    }

    private static string SanitizeSegment(string value)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }

    private sealed record BackupListItem(string Path, DateTimeOffset CreatedUtc);
}
