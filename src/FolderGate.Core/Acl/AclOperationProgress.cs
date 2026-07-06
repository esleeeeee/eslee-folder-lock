namespace FolderGate.Core.Acl;

public sealed class AclOperationProgress
{
    public string Phase { get; init; } = string.Empty;

    public int Processed { get; init; }

    public int Total { get; init; }

    public int Failed { get; init; }

    public string? CurrentPath { get; init; }
}
