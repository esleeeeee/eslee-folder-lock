namespace FolderGate.Core.Acl;

public sealed class AclBackupEntry
{
    public string Path { get; set; } = string.Empty;

    public bool IsDirectory { get; set; }

    public string Sddl { get; set; } = string.Empty;

    public DateTimeOffset CapturedUtc { get; set; } = DateTimeOffset.UtcNow;
}
