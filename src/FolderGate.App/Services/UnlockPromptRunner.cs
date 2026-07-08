using System.Diagnostics;
using System.Windows;
using FolderGate.Core.Localization;
using FolderGate.Core.Models;
using FolderGate.Core.Security;
using FolderGate.Core.Storage;
using FolderGate.Core.Validation;

namespace FolderGate.App.Services;

public sealed class UnlockPromptRunner
{
    private readonly ConfigStore _configStore;
    private readonly PasswordService _passwordService = new();
    private readonly ElevatedToolRunner _toolRunner;
    private readonly StartupRelockService _startupRelockService;

    public UnlockPromptRunner(AppPaths paths)
    {
        _configStore = new ConfigStore(paths);
        ToolLocator toolLocator = new(paths);
        _toolRunner = new ElevatedToolRunner(paths, toolLocator);
        _startupRelockService = new StartupRelockService(paths, toolLocator);
    }

    public async Task<int> RunAsync(string targetPath)
    {
        FolderGateConfig config = _configStore.Load();
        RegisteredFolder? folder = FindRegisteredFolder(config, targetPath);
        if (folder is null)
        {
            ShowError($"{AppText.FolderNotRegisteredPrefix}{Environment.NewLine}{targetPath}");
            return 2;
        }

        if (config.Password is null)
        {
            ShowError(AppText.NoPasswordConfigured);
            return 2;
        }

        if (folder.State == FolderLockState.Working)
        {
            ShowError(AppText.FolderIsWorking);
            return 3;
        }

        if (folder.State == FolderLockState.Unlocked)
        {
            ElevatedToolRunner.OpenExplorer(folder.Path);
            return 0;
        }

        PasswordDialog dialog = PasswordDialog.CreateUnlockPasswordPrompt(
            AppText.UnlockTitle,
            AppText.LanguageCode == "en"
                ? $"Locked folder: {folder.Path}{Environment.NewLine}{Environment.NewLine}{AppText.UnlockPasswordMessage}"
                : $"잠긴 폴더: {folder.Path}{Environment.NewLine}{Environment.NewLine}{AppText.UnlockPasswordMessage}");

        if (dialog.ShowDialog() != true)
        {
            return 1;
        }

        if (!_passwordService.Verify(dialog.Password, config.Password))
        {
            ShowError(AppText.InvalidPasswordNoAcl);
            return 4;
        }

        string operationId = Guid.NewGuid().ToString("N");
        TimeSpan? duration = dialog.SelectedUnlockDuration;
        if (duration is not null)
        {
            return await StartTemporaryUnlockAsync(folder, operationId, duration.Value).ConfigureAwait(true);
        }

        int exitCode;
        try
        {
            exitCode = await _toolRunner.RunHelperAsync("unlock", folder, operationId).ConfigureAwait(true);
        }
        catch (InvalidOperationException ex)
        {
            ShowError(ex.Message);
            return 6;
        }

        if (exitCode != 0)
        {
            ShowError(AppText.LanguageCode == "en"
                ? $"Unlock failed. Exit code: {exitCode}"
                : $"잠금 해제에 실패했습니다. 종료 코드: {exitCode}");
            return exitCode;
        }

        FolderGateConfig refreshedConfig = _configStore.Load();
        RegisteredFolder? refreshed = refreshedConfig.Folders.FirstOrDefault(item => string.Equals(item.Id, folder.Id, StringComparison.OrdinalIgnoreCase));
        if (refreshed is { State: FolderLockState.Unlocked })
        {
            ElevatedToolRunner.OpenExplorer(refreshed.Path);
            return 0;
        }

        ShowError(AppText.UnlockCompletedButStateNotUpdated);
        return 5;
    }

    private async Task<int> StartTemporaryUnlockAsync(RegisteredFolder folder, string operationId, TimeSpan duration)
    {
        Process process;
        try
        {
            _startupRelockService.Install();
            process = _toolRunner.StartHelper("temporary-unlock", folder, operationId, duration: duration);
        }
        catch (InvalidOperationException ex)
        {
            ShowError(ex.Message);
            return 6;
        }

        using (process)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(10);

            while (DateTimeOffset.UtcNow < deadline)
            {
                FolderGateConfig config = _configStore.Load();
                RegisteredFolder? refreshed = config.Folders.FirstOrDefault(item => string.Equals(item.Id, folder.Id, StringComparison.OrdinalIgnoreCase));
                if (refreshed is { State: FolderLockState.TemporarilyUnlocked })
                {
                    ElevatedToolRunner.OpenExplorer(refreshed.Path);
                    return 0;
                }

                if (process.HasExited)
                {
                    int exitCode = process.ExitCode;
                    ShowError(AppText.LanguageCode == "en"
                        ? $"Temporary unlock process exited before completion. Exit code: {exitCode}"
                        : $"임시 잠금 해제 프로세스가 완료되기 전에 종료되었습니다. 종료 코드: {exitCode}");
                    return exitCode == 0 ? 5 : exitCode;
                }

                await Task.Delay(500).ConfigureAwait(true);
            }

            ShowError(AppText.TempUnlockCheckFailed);
            return 7;
        }
    }

    private static RegisteredFolder? FindRegisteredFolder(FolderGateConfig config, string targetPath)
    {
        string normalizedTarget;
        try
        {
            normalizedTarget = WindowsPathComparer.Normalize(targetPath);
        }
        catch (ArgumentException)
        {
            return null;
        }

        return config.Folders.FirstOrDefault(folder => WindowsPathComparer.AreSamePath(folder.Path, normalizedTarget));
    }

    private static void ShowError(string message)
    {
        System.Windows.MessageBox.Show(message, AppText.ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
