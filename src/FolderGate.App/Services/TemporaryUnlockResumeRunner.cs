using System.Threading;
using FolderGate.Core.Localization;
using FolderGate.Core.Models;
using FolderGate.Core.Storage;

namespace FolderGate.App.Services;

public sealed class TemporaryUnlockResumeRunner
{
    private const string MutexName = @"Local\eslee-folder-locker-temporary-resume";

    private readonly ConfigStore _configStore;
    private readonly ElevatedToolRunner _toolRunner;

    public TemporaryUnlockResumeRunner(AppPaths paths)
    {
        _configStore = new ConfigStore(paths);
        _toolRunner = new ElevatedToolRunner(paths, new ToolLocator(paths));
    }

    public async Task<int> RunAsync()
    {
        using Mutex mutex = new(initiallyOwned: true, MutexName, out bool ownsMutex);
        if (!ownsMutex)
        {
            return 0;
        }

        try
        {
            while (true)
            {
                RegisteredFolder? next = FindNextTemporaryUnlock(_configStore.Load());
                if (next is null)
                {
                    return 0;
                }

                DateTimeOffset relockAtUtc = next.TemporaryUnlockUntilUtc ?? DateTimeOffset.UtcNow;
                TimeSpan delay = relockAtUtc - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay).ConfigureAwait(true);
                }

                FolderGateConfig refreshedConfig = _configStore.Load();
                RegisteredFolder? due = refreshedConfig.Folders.FirstOrDefault(folder =>
                    string.Equals(folder.Id, next.Id, StringComparison.OrdinalIgnoreCase) &&
                    folder.State == FolderLockState.TemporarilyUnlocked);

                if (due is null || due.TemporaryUnlockUntilUtc is null || due.TemporaryUnlockUntilUtc > DateTimeOffset.UtcNow)
                {
                    continue;
                }

                string operationId = Guid.NewGuid().ToString("N");
                try
                {
                    int exitCode = await _toolRunner.RunHelperAsync("lock", due, operationId, due.Mode).ConfigureAwait(true);
                    if (exitCode != 0)
                    {
                        MarkAutoRelockFailure(due.Id, $"{AppText.HelperExitCodePrefix} {exitCode}");
                        return exitCode;
                    }
                }
                catch (InvalidOperationException ex)
                {
                    MarkAutoRelockFailure(due.Id, ex.Message);
                    return 2;
                }
            }
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    public static RegisteredFolder? FindNextTemporaryUnlock(FolderGateConfig config)
    {
        return config.Folders
            .Where(folder => folder.State == FolderLockState.TemporarilyUnlocked)
            .Where(folder => folder.TemporaryUnlockUntilUtc is not null)
            .OrderBy(folder => folder.TemporaryUnlockUntilUtc!.Value)
            .FirstOrDefault();
    }

    private void MarkAutoRelockFailure(string folderId, string message)
    {
        FolderGateConfig config = _configStore.Load();
        RegisteredFolder? folder = config.Folders.FirstOrDefault(item => string.Equals(item.Id, folderId, StringComparison.OrdinalIgnoreCase));
        if (folder is null)
        {
            return;
        }

        folder.LastOperationUtc = DateTimeOffset.UtcNow;
        folder.LastResult = $"{AppText.AutoRelockFailedPrefix}: {message}";
        _configStore.Save(config);
    }
}
