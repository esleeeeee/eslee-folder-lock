namespace FolderGate.Core.Acl;

public sealed class AclOperationResult
{
    public bool Success { get; init; }

    public bool RecoveryRequired { get; init; }

    public string OperationId { get; init; } = string.Empty;

    public string? BackupPath { get; init; }

    public int ProcessedCount { get; init; }

    public int FailedCount { get; init; }

    public string Message { get; init; } = string.Empty;

    public static AclOperationResult Ok(string operationId, string? backupPath, int processedCount, string message)
    {
        return new AclOperationResult
        {
            Success = true,
            OperationId = operationId,
            BackupPath = backupPath,
            ProcessedCount = processedCount,
            Message = message
        };
    }

    public static AclOperationResult Failed(string operationId, string? backupPath, int processedCount, string message, bool recoveryRequired, int failedCount = 0)
    {
        return new AclOperationResult
        {
            Success = false,
            RecoveryRequired = recoveryRequired,
            OperationId = operationId,
            BackupPath = backupPath,
            ProcessedCount = processedCount,
            FailedCount = failedCount,
            Message = message
        };
    }
}
