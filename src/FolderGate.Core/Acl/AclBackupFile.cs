using System.Text.Json.Serialization;
using FolderGate.Core.Models;

namespace FolderGate.Core.Acl;

public sealed class AclBackupFile
{
    public int Version { get; set; } = 1;

    public string OperationId { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public string TargetPath { get; set; } = string.Empty;

    public LockMode Mode { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? TimestampUtc
    {
        get => null;
        set
        {
            if (value is not null)
            {
                CreatedUtc = value.Value;
            }
        }
    }

    public List<AclBackupEntry> Entries { get; set; } = [];
}
