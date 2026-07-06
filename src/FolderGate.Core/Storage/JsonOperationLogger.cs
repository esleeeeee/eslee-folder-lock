using System.Text.Json;

namespace FolderGate.Core.Storage;

public sealed class JsonOperationLogger
{
    private readonly AppPaths _paths;
    private readonly JsonSerializerOptions _jsonOptions = JsonOptionsFactory.Create(indented: false);

    public JsonOperationLogger(AppPaths paths)
    {
        _paths = paths;
    }

    public void Write(OperationLogRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        Directory.CreateDirectory(_paths.LogDirectory);
        string line = JsonSerializer.Serialize(record, _jsonOptions);
        File.AppendAllText(_paths.LogFilePath, line + Environment.NewLine);
    }

    public void Info(string operationId, string targetId, string operation, string? path, string message)
    {
        Write(new OperationLogRecord
        {
            OperationId = operationId,
            TargetId = targetId,
            Operation = operation,
            Path = path,
            Status = "Info",
            Message = message
        });
    }

    public void Failure(string operationId, string targetId, string operation, string? path, Exception exception)
    {
        Write(new OperationLogRecord
        {
            OperationId = operationId,
            TargetId = targetId,
            Operation = operation,
            Path = path,
            Status = "Failed",
            Message = exception.Message,
            ExceptionType = exception.GetType().Name
        });
    }
}
