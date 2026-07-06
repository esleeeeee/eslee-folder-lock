namespace FolderGate.Core.Models;

public sealed class FolderGateConfig
{
    public int Version { get; set; } = 1;

    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public PasswordRecord? Password { get; set; }

    public List<RegisteredFolder> Folders { get; set; } = [];
}
