using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using FolderGate.App.Services;
using FolderGate.Core.Localization;
using FolderGate.Core.Models;
using FolderGate.Core.Security;
using FolderGate.Core.Storage;
using FolderGate.Core.Validation;

namespace FolderGate.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly AppPaths _paths;
    private readonly ConfigStore _configStore;
    private readonly PasswordService _passwordService = new();
    private readonly TargetPathValidator _pathValidator;
    private readonly IUserInteractionService _interaction;
    private readonly ElevatedToolRunner _toolRunner;
    private readonly ExplorerContextMenuService _explorerContextMenu;
    private readonly StartupRelockService _startupRelockService;
    private readonly OperationProgressStore _progressStore;
    private FolderGateConfig _config;
    private FolderItemViewModel? _selectedFolder;
    private LockMode _selectedMode = LockMode.Quick;
    private string _statusMessage = AppText.Ready;
    private bool _isBusy;
    private string? _activeOperationId;
    private int _operationTotal;
    private int _operationCompleted;
    private int _operationFailed;
    private string _operationCurrentPath = "-";
    private string _operationElapsed = "-";
    private string _operationEta = "-";
    private string _operationPhase = "-";
    private double _operationProgressPercent;
    private bool _isProgressIndeterminate;

    public MainViewModel(AppPaths paths, IUserInteractionService interaction, ElevatedToolRunner toolRunner)
    {
        _paths = paths;
        _interaction = interaction;
        _toolRunner = toolRunner;
        ToolLocator toolLocator = new(paths);
        _explorerContextMenu = new ExplorerContextMenuService(paths, toolLocator);
        _startupRelockService = new StartupRelockService(paths, toolLocator);
        _configStore = new ConfigStore(paths);
        _progressStore = new OperationProgressStore(paths);
        _pathValidator = new TargetPathValidator(paths);
        _config = _configStore.Load();

        AddFolderCommand = new AsyncRelayCommand(AddFolderAsync);
        RemoveFolderCommand = new RelayCommand(RemoveSelectedFolder, () => SelectedFolder is not null && !IsBusy);
        LockCommand = new AsyncRelayCommand(LockSelectedFolderAsync, () => SelectedFolder is not null && !IsBusy);
        UnlockCommand = new AsyncRelayCommand(UnlockSelectedFolderAsync, () => SelectedFolder is not null && !IsBusy);
        ChangePasswordCommand = new RelayCommand(ChangePassword, () => !IsBusy);
        ShowLogsCommand = new RelayCommand(() => _interaction.ShowLogFile(_paths.LogFilePath));
        OpenRecoveryToolCommand = new AsyncRelayCommand(OpenRecoveryToolAsync, () => !IsBusy);
        RegisterExplorerMenuCommand = new RelayCommand(RegisterExplorerMenu, () => !IsBusy);
        UnregisterExplorerMenuCommand = new RelayCommand(UnregisterExplorerMenu, () => !IsBusy);
        CancelOperationCommand = new RelayCommand(CancelCurrentOperation, () => IsBusy && _activeOperationId is not null);

        RefreshFolders();
    }

    public ObservableCollection<FolderItemViewModel> Folders { get; } = [];

    public FolderItemViewModel? SelectedFolder
    {
        get => _selectedFolder;
        set
        {
            if (SetProperty(ref _selectedFolder, value) && value is not null)
            {
                SelectedMode = value.Model.Mode;
                RaiseCommandStates();
                OnPropertyChanged(nameof(SelectedPath));
                OnPropertyChanged(nameof(SelectedState));
                OnPropertyChanged(nameof(SelectedLastOperation));
                OnPropertyChanged(nameof(SelectedLastResult));
            }
        }
    }

    public bool IsQuickMode
    {
        get => SelectedMode == LockMode.Quick;
        set
        {
            if (value)
            {
                SelectedMode = LockMode.Quick;
            }
        }
    }

    public bool IsHardenedMode
    {
        get => SelectedMode == LockMode.Hardened;
        set
        {
            if (value)
            {
                SelectedMode = LockMode.Hardened;
            }
        }
    }

    public LockMode SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (SetProperty(ref _selectedMode, value))
            {
                OnPropertyChanged(nameof(IsQuickMode));
                OnPropertyChanged(nameof(IsHardenedMode));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string SelectedPath => SelectedFolder?.Path ?? "-";

    public string SelectedState => SelectedFolder?.StateText ?? "-";

    public string SelectedLastOperation => SelectedFolder?.LastOperationText ?? "-";

    public string SelectedLastResult => SelectedFolder?.LastResult ?? "-";

    public int OperationTotal
    {
        get => _operationTotal;
        set => SetProperty(ref _operationTotal, value);
    }

    public int OperationCompleted
    {
        get => _operationCompleted;
        set => SetProperty(ref _operationCompleted, value);
    }

    public int OperationFailed
    {
        get => _operationFailed;
        set => SetProperty(ref _operationFailed, value);
    }

    public string OperationCurrentPath
    {
        get => _operationCurrentPath;
        set => SetProperty(ref _operationCurrentPath, value);
    }

    public string OperationElapsed
    {
        get => _operationElapsed;
        set => SetProperty(ref _operationElapsed, value);
    }

    public string OperationEta
    {
        get => _operationEta;
        set => SetProperty(ref _operationEta, value);
    }

    public string OperationPhase
    {
        get => _operationPhase;
        set => SetProperty(ref _operationPhase, value);
    }

    public double OperationProgressPercent
    {
        get => _operationProgressPercent;
        set => SetProperty(ref _operationProgressPercent, value);
    }

    public bool IsProgressIndeterminate
    {
        get => _isProgressIndeterminate;
        set => SetProperty(ref _isProgressIndeterminate, value);
    }

    public AsyncRelayCommand AddFolderCommand { get; }

    public RelayCommand RemoveFolderCommand { get; }

    public AsyncRelayCommand LockCommand { get; }

    public AsyncRelayCommand UnlockCommand { get; }

    public RelayCommand ChangePasswordCommand { get; }

    public RelayCommand ShowLogsCommand { get; }

    public AsyncRelayCommand OpenRecoveryToolCommand { get; }

    public RelayCommand RegisterExplorerMenuCommand { get; }

    public RelayCommand UnregisterExplorerMenuCommand { get; }

    public RelayCommand CancelOperationCommand { get; }

    private async Task AddFolderAsync()
    {
        string? selectedPath = _interaction.SelectFolder();
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        PathValidationResult validation = _pathValidator.ValidateDirectory(selectedPath);
        if (!validation.IsValid || validation.NormalizedPath is null)
        {
            _interaction.ShowError(validation.ErrorMessage ?? AppText.InvalidFolderGeneric);
            return;
        }

        if (_config.Password is null)
        {
            string? newPassword = _interaction.AskNewPassword(AppText.NewPasswordTitle, AppText.NewPasswordMessage);
            if (newPassword is null)
            {
                return;
            }

            _config.Password = _passwordService.CreatePasswordRecord(newPassword);
        }

        if (_config.Folders.Any(folder => WindowsPathComparer.AreSamePath(folder.Path, validation.NormalizedPath)))
        {
            _interaction.ShowInfo(AppText.AlreadyRegisteredFolder);
            return;
        }

        RegisteredFolder registeredFolder = new()
        {
            DisplayName = new DirectoryInfo(validation.NormalizedPath).Name,
            Path = validation.NormalizedPath,
            Mode = SelectedMode,
            State = FolderLockState.Unlocked,
            OwnerSid = WindowsIdentity.GetCurrent().User?.Value ?? string.Empty,
            HasReparsePointWarning = validation.Warnings.Count > 0,
            LastOperationUtc = DateTimeOffset.UtcNow,
            LastResult = validation.Warnings.Count > 0 ? validation.Warnings[0] : AppText.Registered
        };

        _config.Folders.Add(registeredFolder);
        _configStore.Save(_config);
        RefreshFolders(registeredFolder.Id);
        StatusMessage = AppText.FolderRegistered;
        await Task.CompletedTask;
    }

    private void RemoveSelectedFolder()
    {
        if (SelectedFolder is null)
        {
            return;
        }

        if (SelectedFolder.Model.State is FolderLockState.Locked or FolderLockState.Working)
        {
            _interaction.ShowError(AppText.RemoveLockedFolderError);
            return;
        }

        if (!_interaction.Confirm(AppText.RemoveFolderTitle, AppText.RemoveFolderMessage))
        {
            return;
        }

        _config.Folders.RemoveAll(folder => string.Equals(folder.Id, SelectedFolder.Id, StringComparison.OrdinalIgnoreCase));
        _configStore.Save(_config);
        RefreshFolders();
        StatusMessage = AppText.FolderRegistrationRemoved;
    }

    private async Task LockSelectedFolderAsync()
    {
        if (SelectedFolder is null)
        {
            return;
        }

        RegisteredFolder folder = SelectedFolder.Model;
        string modeText = AppText.ModeName(SelectedMode);
        string childText = SelectedMode == LockMode.Quick ? AppText.NoChildItems : AppText.RecursiveChildAcl;
        string recoveryToolPath = TryFindToolPath("FolderGate.RecoveryTool");

        string message =
            $"{AppText.ActualPathLabel}: {folder.Path}{Environment.NewLine}" +
            $"{AppText.ModeLabel}: {modeText}{Environment.NewLine}" +
            $"{AppText.ScopeLabel}: {childText}{Environment.NewLine}" +
            $"{AppText.RecoveryToolName}: {recoveryToolPath}{Environment.NewLine}{Environment.NewLine}" +
            AppText.LockConfirmTail;

        if (!_interaction.Confirm(AppText.LockConfirmTitle, message))
        {
            return;
        }

        await RunHelperAndRefreshAsync("lock", folder, SelectedMode).ConfigureAwait(true);
    }

    private async Task UnlockSelectedFolderAsync()
    {
        if (SelectedFolder is null)
        {
            return;
        }

        UnlockPasswordRequest? request = _interaction.AskUnlockPassword(AppText.UnlockTitle, AppText.UnlockPasswordMessage);
        if (request is null)
        {
            return;
        }

        if (!_passwordService.Verify(request.Password, _config.Password))
        {
            _interaction.ShowError(AppText.InvalidPasswordNoAcl);
            return;
        }

        RegisteredFolder folder = SelectedFolder.Model;
        if (request.Duration is not null)
        {
            await StartTemporaryUnlockAndRefreshAsync(folder, request.Duration.Value).ConfigureAwait(true);
            return;
        }

        await RunHelperAndRefreshAsync("unlock", folder, null).ConfigureAwait(true);

        _config = _configStore.Load();
        RegisteredFolder? refreshed = _config.Folders.FirstOrDefault(item => item.Id == folder.Id);
        if (refreshed is { State: FolderLockState.Unlocked } && Directory.Exists(refreshed.Path))
        {
            ElevatedToolRunner.OpenExplorer(refreshed.Path);
        }
    }

    private async Task StartTemporaryUnlockAndRefreshAsync(RegisteredFolder folder, TimeSpan duration)
    {
        string operationId = Guid.NewGuid().ToString("N");
        Process? process = null;
        try
        {
            IsBusy = true;
            _activeOperationId = operationId;
            ResetOperationProgress();
            StatusMessage = AppText.TempUnlockRequestingUac;
            _startupRelockService.Install();
            process = _toolRunner.StartHelper("temporary-unlock", folder, operationId, duration: duration);

            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMinutes(10);
            while (DateTimeOffset.UtcNow < deadline)
            {
                ApplyProgressSnapshot(operationId);
                _config = _configStore.Load();
                RegisteredFolder? refreshed = _config.Folders.FirstOrDefault(item => item.Id == folder.Id);
                if (refreshed is { State: FolderLockState.TemporarilyUnlocked })
                {
                    RefreshFolders(folder.Id);
                    StatusMessage = AppText.TempUnlockStarted;
                    ElevatedToolRunner.OpenExplorer(refreshed.Path);
                    return;
                }

                if (process.HasExited)
                {
                    int exitCode = process.ExitCode;
                    RefreshFolders(folder.Id);
                    StatusMessage = AppText.LanguageCode == "en"
                        ? $"Temporary unlock process exited. Exit code: {exitCode}"
                        : $"임시 잠금 해제 프로세스가 종료되었습니다. 종료 코드: {exitCode}";
                    if (exitCode != 0)
                    {
                        _interaction.ShowError(AppText.TempUnlockFailed);
                    }

                    return;
                }

                await Task.Delay(500).ConfigureAwait(true);
            }

            RefreshFolders(folder.Id);
            StatusMessage = AppText.TempUnlockCheckTimeout;
            _interaction.ShowError(AppText.TempUnlockCheckFailed);
        }
        catch (Exception ex)
        {
            _config = _configStore.Load();
            RefreshFolders(folder.Id);
            _interaction.ShowError(ex.Message);
            StatusMessage = AppText.TempUnlockGeneralFailure;
        }
        finally
        {
            process?.Dispose();
            _activeOperationId = null;
            IsProgressIndeterminate = false;
            IsBusy = false;
        }
    }

    private void ChangePassword()
    {
        if (_config.Password is not null)
        {
            string? oldPassword = _interaction.AskPassword(AppText.CurrentPasswordTitle, AppText.CurrentPasswordMessage);
            if (oldPassword is null)
            {
                return;
            }

            if (!_passwordService.Verify(oldPassword, _config.Password))
            {
                _interaction.ShowError(AppText.InvalidPasswordNoAcl);
                return;
            }
        }

        string? newPassword = _interaction.AskNewPassword(AppText.ChangePasswordTitle, AppText.ChangePasswordMessage);
        if (newPassword is null)
        {
            return;
        }

        _config.Password = _passwordService.CreatePasswordRecord(newPassword);
        _configStore.Save(_config);
        StatusMessage = AppText.PasswordChanged;
    }

    private async Task OpenRecoveryToolAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = AppText.RecoveryToolRunning;
            await _toolRunner.OpenRecoveryToolAsync().ConfigureAwait(true);
            _config = _configStore.Load();
            RefreshFolders(SelectedFolder?.Id);
            StatusMessage = AppText.RecoveryToolFinished;
        }
        catch (Exception ex)
        {
            _interaction.ShowError(ex.Message);
            StatusMessage = AppText.RecoveryToolFailed;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RegisterExplorerMenu()
    {
        try
        {
            _explorerContextMenu.Install();
            StatusMessage = AppText.ExplorerMenuRegistered;
            _interaction.ShowInfo(AppText.ExplorerMenuRegisteredInfo);
        }
        catch (Exception ex)
        {
            _interaction.ShowError(ex.Message);
            StatusMessage = AppText.ExplorerMenuRegisterFailed;
        }
    }

    private void UnregisterExplorerMenu()
    {
        try
        {
            _explorerContextMenu.Uninstall();
            StatusMessage = AppText.ExplorerMenuRemoved;
            _interaction.ShowInfo(AppText.ExplorerMenuRemovedInfo);
        }
        catch (Exception ex)
        {
            _interaction.ShowError(ex.Message);
            StatusMessage = AppText.ExplorerMenuRemoveFailed;
        }
    }

    private async Task RunHelperAndRefreshAsync(string command, RegisteredFolder folder, LockMode? mode)
    {
        string operationId = Guid.NewGuid().ToString("N");
        try
        {
            IsBusy = true;
            _activeOperationId = operationId;
            ResetOperationProgress();
            StatusMessage = AppText.UacRunStatus;
            Task<int> helperTask = _toolRunner.RunHelperAsync(command, folder, operationId, mode);
            while (!helperTask.IsCompleted)
            {
                ApplyProgressSnapshot(operationId);
                await Task.Delay(350).ConfigureAwait(true);
            }

            int exitCode = await helperTask.ConfigureAwait(true);
            ApplyProgressSnapshot(operationId);
            _config = _configStore.Load();
            RefreshFolders(folder.Id);

            if (exitCode == 0)
            {
                StatusMessage = AppText.OperationCompleted;
            }
            else
            {
                StatusMessage = AppText.LanguageCode == "en"
                    ? $"Operation failed. Exit code: {exitCode}"
                    : $"작업이 실패했습니다. 종료 코드: {exitCode}";
                _interaction.ShowError(AppText.OperationFailedCheckLogs);
            }
        }
        catch (Exception ex)
        {
            _config = _configStore.Load();
            RefreshFolders(folder.Id);
            _interaction.ShowError(ex.Message);
            StatusMessage = AppText.OperationFailed;
        }
        finally
        {
            _activeOperationId = null;
            IsProgressIndeterminate = false;
            IsBusy = false;
        }
    }

    private void CancelCurrentOperation()
    {
        if (_activeOperationId is null)
        {
            return;
        }

        _progressStore.RequestCancel(_activeOperationId);
        StatusMessage = AppText.CancelRequested;
        RaiseCommandStates();
    }

    private void ResetOperationProgress()
    {
        OperationTotal = 0;
        OperationCompleted = 0;
        OperationFailed = 0;
        OperationCurrentPath = "-";
        OperationElapsed = "00:00:00";
        OperationEta = "-";
        OperationPhase = AppText.Starting;
        OperationProgressPercent = 0;
        IsProgressIndeterminate = true;
        RaiseCommandStates();
    }

    private void ApplyProgressSnapshot(string operationId)
    {
        OperationProgressSnapshot? snapshot = _progressStore.TryLoadProgress(operationId);
        if (snapshot is null)
        {
            UpdateElapsed(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 0, 0);
            return;
        }

        OperationTotal = snapshot.TotalCount;
        OperationCompleted = snapshot.CompletedCount;
        OperationFailed = snapshot.FailedCount;
        OperationCurrentPath = string.IsNullOrWhiteSpace(snapshot.CurrentPath) ? "-" : snapshot.CurrentPath;
        OperationPhase = AppText.FormatPhase(snapshot.Phase);
        IsProgressIndeterminate = snapshot.TotalCount <= 0;
        OperationProgressPercent = snapshot.TotalCount <= 0
            ? 0
            : Math.Clamp(snapshot.CompletedCount * 100.0 / snapshot.TotalCount, 0, 100);

        UpdateElapsed(snapshot.StartedUtc, DateTimeOffset.UtcNow, snapshot.CompletedCount, snapshot.TotalCount);
        StatusMessage = snapshot.Message;
    }

    private void UpdateElapsed(DateTimeOffset startedUtc, DateTimeOffset nowUtc, int completed, int total)
    {
        TimeSpan elapsed = nowUtc - startedUtc;
        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        OperationElapsed = FormatDuration(elapsed);
        if (completed > 0 && total > completed)
        {
            double secondsPerItem = elapsed.TotalSeconds / completed;
            OperationEta = FormatDuration(TimeSpan.FromSeconds(secondsPerItem * (total - completed)));
        }
        else if (total > 0 && completed >= total)
        {
            OperationEta = "00:00:00";
        }
        else
        {
            OperationEta = "-";
        }
    }

    private static string FormatDuration(TimeSpan value)
    {
        return value.TotalHours >= 1
            ? value.ToString(@"hh\:mm\:ss")
            : value.ToString(@"mm\:ss");
    }

    private void RefreshFolders(string? selectedId = null)
    {
        _config = _configStore.Load();
        Folders.Clear();
        foreach (RegisteredFolder folder in _config.Folders.OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase))
        {
            Folders.Add(new FolderItemViewModel(folder));
        }

        SelectedFolder = selectedId is null
            ? Folders.FirstOrDefault()
            : Folders.FirstOrDefault(item => string.Equals(item.Id, selectedId, StringComparison.OrdinalIgnoreCase)) ?? Folders.FirstOrDefault();

        OnPropertyChanged(nameof(SelectedPath));
        OnPropertyChanged(nameof(SelectedState));
        OnPropertyChanged(nameof(SelectedLastOperation));
        OnPropertyChanged(nameof(SelectedLastResult));
    }

    private string TryFindToolPath(string projectName)
    {
        try
        {
            return new ToolLocator(_paths).FindExecutable(projectName);
        }
        catch
        {
            return AppText.ToolNotFoundHint;
        }
    }

    private void RaiseCommandStates()
    {
        RemoveFolderCommand.RaiseCanExecuteChanged();
        LockCommand.RaiseCanExecuteChanged();
        UnlockCommand.RaiseCanExecuteChanged();
        ChangePasswordCommand.RaiseCanExecuteChanged();
        OpenRecoveryToolCommand.RaiseCanExecuteChanged();
        RegisterExplorerMenuCommand.RaiseCanExecuteChanged();
        UnregisterExplorerMenuCommand.RaiseCanExecuteChanged();
        CancelOperationCommand.RaiseCanExecuteChanged();
    }
}
