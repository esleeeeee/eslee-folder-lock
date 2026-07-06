namespace FolderGate.Core.Models;

public sealed class RegisteredFolder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string DisplayName { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string OwnerSid { get; set; } = string.Empty;

    public LockMode Mode { get; set; } = LockMode.Quick;

    public FolderLockState State { get; set; } = FolderLockState.Unlocked;

    public string? LatestBackupPath { get; set; }

    public string? LastOperationId { get; set; }

    public DateTimeOffset? LastOperationUtc { get; set; }

    public string? LastResult { get; set; }

    public bool HasReparsePointWarning { get; set; }
}
