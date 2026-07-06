namespace FolderGate.Core.Storage;

public sealed class OperationProgressSnapshot
{
    public string OperationId { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public string Operation { get; set; } = string.Empty;

    public string Phase { get; set; } = "starting";

    public int TotalCount { get; set; }

    public int CompletedCount { get; set; }

    public int FailedCount { get; set; }

    public string? CurrentPath { get; set; }

    public DateTimeOffset StartedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public bool IsCompleted { get; set; }

    public bool IsCancellationRequested { get; set; }

    public string Message { get; set; } = string.Empty;
}
