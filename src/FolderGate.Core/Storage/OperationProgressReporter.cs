using FolderGate.Core.Acl;

namespace FolderGate.Core.Storage;

public sealed class OperationProgressReporter : IProgress<AclOperationProgress>, IDisposable
{
    private readonly OperationProgressStore _store;
    private readonly TimeSpan _minimumWriteInterval;
    private readonly int _minimumItemBatch;
    private readonly object _sync = new();
    private readonly OperationProgressSnapshot _snapshot;
    private DateTimeOffset _lastWriteUtc = DateTimeOffset.MinValue;
    private int _lastWrittenCompleted;
    private string _lastWrittenPhase = string.Empty;
    private bool _disposed;

    public OperationProgressReporter(
        OperationProgressStore store,
        string operationId,
        string targetId,
        string operation,
        TimeSpan? minimumWriteInterval = null,
        int minimumItemBatch = 100)
    {
        _store = store;
        _minimumWriteInterval = minimumWriteInterval ?? TimeSpan.FromMilliseconds(300);
        _minimumItemBatch = minimumItemBatch;
        _snapshot = new OperationProgressSnapshot
        {
            OperationId = operationId,
            TargetId = targetId,
            Operation = operation,
            StartedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow,
            Message = "작업을 시작합니다."
        };
        ForceSave();
    }

    public void Report(AclOperationProgress value)
    {
        lock (_sync)
        {
            _snapshot.Phase = value.Phase;
            _snapshot.TotalCount = value.Total;
            _snapshot.CompletedCount = value.Processed;
            _snapshot.FailedCount = value.Failed;
            _snapshot.CurrentPath = value.CurrentPath;
            _snapshot.UpdatedUtc = DateTimeOffset.UtcNow;
            _snapshot.Message = PhaseToMessage(value.Phase);

            bool phaseChanged = !string.Equals(_lastWrittenPhase, value.Phase, StringComparison.Ordinal);
            bool enoughItems = Math.Abs(value.Processed - _lastWrittenCompleted) >= _minimumItemBatch;
            bool enoughTime = DateTimeOffset.UtcNow - _lastWriteUtc >= _minimumWriteInterval;

            if (phaseChanged || enoughItems || enoughTime || value.Failed > 0)
            {
                SaveCurrent();
            }
        }
    }

    public void MarkCancellationRequested()
    {
        lock (_sync)
        {
            _snapshot.IsCancellationRequested = true;
            _snapshot.UpdatedUtc = DateTimeOffset.UtcNow;
            _snapshot.Message = "취소 요청을 확인했습니다. 가능한 범위에서 원복 중입니다.";
            SaveCurrent();
        }
    }

    public void Complete(string message, bool success)
    {
        lock (_sync)
        {
            _snapshot.IsCompleted = true;
            _snapshot.UpdatedUtc = DateTimeOffset.UtcNow;
            _snapshot.Message = message;
            if (!success && _snapshot.FailedCount == 0)
            {
                _snapshot.FailedCount = 1;
            }

            SaveCurrent();
        }
    }

    public void ForceSave()
    {
        lock (_sync)
        {
            SaveCurrent();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ForceSave();
        _disposed = true;
    }

    private void SaveCurrent()
    {
        _store.SaveProgress(_snapshot);
        _lastWriteUtc = DateTimeOffset.UtcNow;
        _lastWrittenCompleted = _snapshot.CompletedCount;
        _lastWrittenPhase = _snapshot.Phase;
    }

    private static string PhaseToMessage(string phase)
    {
        return phase switch
        {
            "scan" => "처리할 항목 수를 계산하는 중입니다.",
            "backup" => "ACL 백업을 메모리에 수집하는 중입니다.",
            "lock" => "ACL 잠금을 적용하는 중입니다.",
            "unlock" => "이은성폴더잠금기(FolderGate 엔진)가 추가한 ACL 규칙을 제거하는 중입니다.",
            "restore" => "백업 ACL을 복구하는 중입니다.",
            "rollback" => "이미 변경된 항목을 원복하는 중입니다.",
            _ => "작업 중입니다."
        };
    }
}
