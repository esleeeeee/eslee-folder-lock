namespace FolderGate.Core.Storage;

public sealed class OperationLogRecord
{
    public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;

    public string OperationId { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public string Operation { get; set; } = string.Empty;

    public string? Path { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Message { get; set; }

    public string? ExceptionType { get; set; }
}
