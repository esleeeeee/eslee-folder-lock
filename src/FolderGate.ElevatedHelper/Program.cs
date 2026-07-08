using System.Text;
using FolderGate.Core.Acl;
using FolderGate.Core.Localization;
using FolderGate.Core.Models;
using FolderGate.Core.Security;
using FolderGate.Core.Storage;

Console.OutputEncoding = Encoding.UTF8;
Console.Title = AppText.ElevatedHelperName;

try
{
    CommandLine commandLine = CommandLine.Parse(args);
    if (commandLine.Command is null)
    {
        PrintUsage();
        return 1;
    }

    if (!WindowsElevation.IsCurrentProcessElevated())
    {
        Console.Error.WriteLine(AppText.HelperRequiresAdmin);
        return 740;
    }

    AppPaths paths = AppPaths.Resolve(commandLine.GetValue("root"));
    ConfigStore configStore = new(paths);
    FolderGateConfig config = configStore.Load();

    string targetId = commandLine.GetRequiredValue("target-id");
    RegisteredFolder folder = config.Folders.FirstOrDefault(item => string.Equals(item.Id, targetId, StringComparison.OrdinalIgnoreCase))
        ?? throw new InvalidOperationException(AppText.RegisteredTargetNotFound);

    AclService aclService = new(paths);
    string operationId = commandLine.GetValue("operation-id") ?? Guid.NewGuid().ToString("N");
    OperationProgressStore progressStore = new(paths);
    progressStore.ClearCancel(operationId);
    using CancellationTokenSource cancellationTokenSource = new();
    using OperationProgressReporter progressReporter = new(progressStore, operationId, folder.Id, commandLine.Command);
    using CancellationWatcher cancellationWatcher = CancellationWatcher.Start(progressStore, operationId, cancellationTokenSource, progressReporter);
    ConsoleProgressReporter progress = new(progressReporter);

    AclOperationResult result = commandLine.Command.ToLowerInvariant() switch
    {
        "lock" => RunLock(configStore, config, folder, aclService, commandLine, progress, cancellationTokenSource.Token, operationId),
        "unlock" => RunUnlock(configStore, config, folder, aclService, progress, cancellationTokenSource.Token, operationId),
        "temporary-unlock" => RunTemporaryUnlock(configStore, config, folder, aclService, commandLine, progress, cancellationTokenSource.Token, operationId),
        "restore" => RunRestore(configStore, config, folder, aclService, commandLine, progress, cancellationTokenSource.Token, operationId),
        _ => throw new InvalidOperationException(AppText.UnknownCommand)
    };

    progressReporter.Complete(result.Message, result.Success);
    progressStore.ClearCancel(operationId);
    Console.WriteLine(result.Message);
    return result.Success ? 0 : 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static AclOperationResult RunLock(
    ConfigStore configStore,
    FolderGateConfig config,
    RegisteredFolder folder,
    AclService aclService,
    CommandLine commandLine,
    IProgress<AclOperationProgress> progress,
    CancellationToken cancellationToken,
    string operationId)
{
    LockMode mode = Enum.TryParse(commandLine.GetValue("mode"), ignoreCase: true, out LockMode parsed)
        ? parsed
        : folder.Mode;

    folder.Mode = mode;
    folder.State = FolderLockState.Working;
    folder.LastOperationUtc = DateTimeOffset.UtcNow;
    folder.TemporaryUnlockUntilUtc = null;
    folder.LastResult = AppText.LockWorking;
    configStore.Save(config);

    AclOperationResult result = aclService.LockAsync(folder, mode, cancellationToken, progress, operationId).GetAwaiter().GetResult();

    folder.State = result.Success ? FolderLockState.Locked : FolderLockState.RecoveryRequired;
    folder.LatestBackupPath = result.BackupPath ?? folder.LatestBackupPath;
    folder.LastOperationId = result.OperationId;
    folder.LastOperationUtc = DateTimeOffset.UtcNow;
    folder.TemporaryUnlockUntilUtc = null;
    folder.LastResult = result.Message;
    configStore.Save(config);
    return result;
}

static AclOperationResult RunUnlock(
    ConfigStore configStore,
    FolderGateConfig config,
    RegisteredFolder folder,
    AclService aclService,
    IProgress<AclOperationProgress> progress,
    CancellationToken cancellationToken,
    string operationId)
{
    folder.State = FolderLockState.Working;
    folder.LastOperationUtc = DateTimeOffset.UtcNow;
    folder.TemporaryUnlockUntilUtc = null;
    folder.LastResult = AppText.UnlockWorking;
    configStore.Save(config);

    AclOperationResult result = aclService.UnlockAsync(folder, folder.LatestBackupPath, cancellationToken, progress, operationId).GetAwaiter().GetResult();

    folder.State = result.Success ? FolderLockState.Unlocked : FolderLockState.RecoveryRequired;
    folder.LastOperationId = result.OperationId;
    folder.LastOperationUtc = DateTimeOffset.UtcNow;
    folder.TemporaryUnlockUntilUtc = null;
    folder.LastResult = result.Message;
    configStore.Save(config);
    return result;
}

static AclOperationResult RunTemporaryUnlock(
    ConfigStore configStore,
    FolderGateConfig config,
    RegisteredFolder folder,
    AclService aclService,
    CommandLine commandLine,
    IProgress<AclOperationProgress> progress,
    CancellationToken cancellationToken,
    string operationId)
{
    TimeSpan duration = ParseDuration(commandLine.GetRequiredValue("duration-seconds"));

    folder.State = FolderLockState.Working;
    folder.LastOperationUtc = DateTimeOffset.UtcNow;
    folder.TemporaryUnlockUntilUtc = null;
    folder.LastResult = AppText.TemporaryUnlockWorking;
    configStore.Save(config);

    AclOperationResult unlockResult = aclService.UnlockAsync(folder, folder.LatestBackupPath, cancellationToken, progress, operationId).GetAwaiter().GetResult();
    if (!unlockResult.Success)
    {
        folder.State = FolderLockState.RecoveryRequired;
        folder.LastOperationId = unlockResult.OperationId;
        folder.LastOperationUtc = DateTimeOffset.UtcNow;
        folder.LastResult = unlockResult.Message;
        configStore.Save(config);
        return unlockResult;
    }

    DateTimeOffset relockAtUtc = DateTimeOffset.UtcNow.Add(duration);
    folder.State = FolderLockState.TemporarilyUnlocked;
    folder.LastOperationId = unlockResult.OperationId;
    folder.LastOperationUtc = DateTimeOffset.UtcNow;
    folder.TemporaryUnlockUntilUtc = relockAtUtc;
    folder.LastResult = AppText.FormatTemporaryUnlockResult(duration);
    configStore.Save(config);

    progress.Report(new AclOperationProgress
    {
        Phase = "temporary-unlock-wait",
        Processed = 0,
        Total = 1,
        Failed = 0,
        CurrentPath = folder.Path
    });

    try
    {
        Task.Delay(duration, cancellationToken).GetAwaiter().GetResult();
    }
    catch (OperationCanceledException)
    {
        folder.State = FolderLockState.TemporarilyUnlocked;
        folder.LastOperationUtc = DateTimeOffset.UtcNow;
        folder.LastResult = AppText.TemporaryUnlockWaitCanceled;
        configStore.Save(config);
        return AclOperationResult.Failed(operationId, unlockResult.BackupPath, unlockResult.ProcessedCount, folder.LastResult, recoveryRequired: false);
    }

    folder.State = FolderLockState.Working;
    folder.LastOperationUtc = DateTimeOffset.UtcNow;
    folder.LastResult = AppText.TemporaryUnlockRelocking;
    configStore.Save(config);

    AclOperationResult lockResult = aclService.LockAsync(folder, folder.Mode, cancellationToken, progress, operationId).GetAwaiter().GetResult();

    folder.State = lockResult.Success ? FolderLockState.Locked : FolderLockState.RecoveryRequired;
    folder.LatestBackupPath = lockResult.BackupPath ?? folder.LatestBackupPath;
    folder.LastOperationId = lockResult.OperationId;
    folder.LastOperationUtc = DateTimeOffset.UtcNow;
    folder.TemporaryUnlockUntilUtc = null;
    folder.LastResult = lockResult.Success
        ? AppText.TemporaryUnlockRelocked
        : lockResult.Message;
    configStore.Save(config);

    return lockResult.Success
        ? AclOperationResult.Ok(operationId, lockResult.BackupPath, lockResult.ProcessedCount, AppText.TemporaryUnlockRelocked)
        : lockResult;
}

static AclOperationResult RunRestore(
    ConfigStore configStore,
    FolderGateConfig config,
    RegisteredFolder folder,
    AclService aclService,
    CommandLine commandLine,
    IProgress<AclOperationProgress> progress,
    CancellationToken cancellationToken,
    string operationId)
{
    string backupPath = commandLine.GetValue("backup") ?? folder.LatestBackupPath
        ?? throw new InvalidOperationException(AppText.MissingAclBackup);

    folder.State = FolderLockState.Working;
    folder.LastOperationUtc = DateTimeOffset.UtcNow;
    folder.LastResult = AppText.RestoreWorking;
    configStore.Save(config);

    AclOperationResult result = aclService.RestoreBackupAsync(folder.Id, backupPath, cancellationToken, progress, operationId).GetAwaiter().GetResult();

    folder.State = result.Success ? FolderLockState.Unlocked : FolderLockState.RecoveryRequired;
    folder.LastOperationId = result.OperationId;
    folder.LastOperationUtc = DateTimeOffset.UtcNow;
    folder.LastResult = result.Message;
    configStore.Save(config);
    return result;
}

static void PrintUsage()
{
    string exeName = AppText.LanguageCode == "en" ? "eslee-folder-locker-helper.exe" : "eslee폴더잠금기_권한도우미.exe";
    Console.WriteLine($"{exeName} lock --root <project-root> --target-id <id> --operation-id <id> --mode Quick|Hardened");
    Console.WriteLine($"{exeName} unlock --root <project-root> --target-id <id> --operation-id <id>");
    Console.WriteLine($"{exeName} temporary-unlock --root <project-root> --target-id <id> --operation-id <id> --duration-seconds <seconds>");
    Console.WriteLine($"{exeName} restore --root <project-root> --target-id <id> --operation-id <id> --backup <backup-json>");
}

static TimeSpan ParseDuration(string value)
{
    if (!int.TryParse(value, out int seconds) || seconds <= 0)
    {
        throw new InvalidOperationException(AppText.InvalidDurationSeconds);
    }

    return TimeSpan.FromSeconds(seconds);
}

internal sealed class ConsoleProgressReporter : IProgress<AclOperationProgress>
{
    private readonly OperationProgressReporter _fileReporter;

    public ConsoleProgressReporter(OperationProgressReporter fileReporter)
    {
        _fileReporter = fileReporter;
    }

    public void Report(AclOperationProgress value)
    {
        _fileReporter.Report(value);
        Console.WriteLine($"{value.Phase}: {value.Processed}/{value.Total} failed={value.Failed} {value.CurrentPath}");
    }
}

internal sealed class CancellationWatcher : IDisposable
{
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly Timer _timer;
    private readonly OperationProgressReporter? _progressReporter;
    private bool _reported;

    private CancellationWatcher(OperationProgressStore store, string operationId, CancellationTokenSource cancellationTokenSource, OperationProgressReporter? progressReporter)
    {
        _cancellationTokenSource = cancellationTokenSource;
        _progressReporter = progressReporter;
        _timer = new Timer(_ =>
        {
            if (store.IsCancellationRequested(operationId) && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                if (!_reported)
                {
                    _progressReporter?.MarkCancellationRequested();
                    _reported = true;
                }
            }
        }, null, TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));
    }

    public static CancellationWatcher Start(OperationProgressStore store, string operationId, CancellationTokenSource cancellationTokenSource, OperationProgressReporter progressReporter)
    {
        return new CancellationWatcher(store, operationId, cancellationTokenSource, progressReporter);
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}

internal sealed class CommandLine
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public string? Command { get; private init; }

    public string? GetValue(string name)
    {
        return _values.TryGetValue(name, out string? value) ? value : null;
    }

    public string GetRequiredValue(string name)
    {
        return GetValue(name) ?? throw new InvalidOperationException(AppText.RequiredArgumentMissing(name));
    }

    public static CommandLine Parse(string[] args)
    {
        CommandLine result = new()
        {
            Command = args.Length > 0 ? args[0] : null
        };

        for (int i = 1; i < args.Length; i++)
        {
            string token = args[i];
            if (!token.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            string key = token[2..];
            string value = i + 1 < args.Length ? args[++i] : string.Empty;
            result._values[key] = value;
        }

        return result;
    }
}
