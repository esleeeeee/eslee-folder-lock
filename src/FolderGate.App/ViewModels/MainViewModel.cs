using System.Collections.ObjectModel;
using System.IO;
using System.Security.Principal;
using FolderGate.App.Services;
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
    private readonly OperationProgressStore _progressStore;
    private FolderGateConfig _config;
    private FolderItemViewModel? _selectedFolder;
    private LockMode _selectedMode = LockMode.Quick;
    private string _statusMessage = "준비됨";
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
            _interaction.ShowError(validation.ErrorMessage ?? "이 폴더는 등록할 수 없습니다.");
            return;
        }

        if (_config.Password is null)
        {
            string? newPassword = _interaction.AskNewPassword("비밀번호 설정", "처음 등록하는 폴더입니다. 잠금 해제에 사용할 비밀번호를 설정하세요. 비밀번호는 최소 4자 이상이어야 합니다.");
            if (newPassword is null)
            {
                return;
            }

            _config.Password = _passwordService.CreatePasswordRecord(newPassword);
        }

        if (_config.Folders.Any(folder => WindowsPathComparer.AreSamePath(folder.Path, validation.NormalizedPath)))
        {
            _interaction.ShowInfo("이미 등록된 폴더입니다.");
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
            LastResult = validation.Warnings.Count > 0 ? validation.Warnings[0] : "등록됨"
        };

        _config.Folders.Add(registeredFolder);
        _configStore.Save(_config);
        RefreshFolders(registeredFolder.Id);
        StatusMessage = "폴더를 등록했습니다.";
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
            _interaction.ShowError("잠긴 폴더는 먼저 잠금 해제한 뒤 제거하세요.");
            return;
        }

        if (!_interaction.Confirm("폴더 제거", "목록에서만 제거합니다. 실제 파일은 수정하지 않습니다. 계속할까요?"))
        {
            return;
        }

        _config.Folders.RemoveAll(folder => string.Equals(folder.Id, SelectedFolder.Id, StringComparison.OrdinalIgnoreCase));
        _configStore.Save(_config);
        RefreshFolders();
        StatusMessage = "폴더 등록을 제거했습니다.";
    }

    private async Task LockSelectedFolderAsync()
    {
        if (SelectedFolder is null)
        {
            return;
        }

        RegisteredFolder folder = SelectedFolder.Model;
        string modeText = SelectedMode == LockMode.Quick ? "빠른 모드" : "강화 모드";
        string childText = SelectedMode == LockMode.Quick ? "하위 항목 처리 없음" : "하위 폴더와 파일 ACL 재귀 처리";
        string recoveryToolPath = TryFindToolPath("FolderGate.RecoveryTool");

        string message =
            $"실제 경로: {folder.Path}{Environment.NewLine}" +
            $"모드: {modeText}{Environment.NewLine}" +
            $"처리 범위: {childText}{Environment.NewLine}" +
            $"이은성폴더잠금기 복구 도구: {recoveryToolPath}{Environment.NewLine}{Environment.NewLine}" +
            "이 작업은 파일 내용을 암호화하거나 이동하지 않고 NTFS 권한만 변경합니다. 계속할까요?";

        if (!_interaction.Confirm("잠금 확인", message))
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

        string? password = _interaction.AskPassword("잠금 해제", "비밀번호를 입력하세요.");
        if (password is null)
        {
            return;
        }

        if (!_passwordService.Verify(password, _config.Password))
        {
            _interaction.ShowError("비밀번호가 올바르지 않습니다. ACL 변경은 수행하지 않았습니다.");
            return;
        }

        RegisteredFolder folder = SelectedFolder.Model;
        await RunHelperAndRefreshAsync("unlock", folder, null).ConfigureAwait(true);

        _config = _configStore.Load();
        RegisteredFolder? refreshed = _config.Folders.FirstOrDefault(item => item.Id == folder.Id);
        if (refreshed is { State: FolderLockState.Unlocked } && Directory.Exists(refreshed.Path))
        {
            ElevatedToolRunner.OpenExplorer(refreshed.Path);
        }
    }

    private void ChangePassword()
    {
        if (_config.Password is not null)
        {
            string? oldPassword = _interaction.AskPassword("비밀번호 확인", "현재 비밀번호를 입력하세요.");
            if (oldPassword is null)
            {
                return;
            }

            if (!_passwordService.Verify(oldPassword, _config.Password))
            {
                _interaction.ShowError("비밀번호가 올바르지 않습니다.");
                return;
            }
        }

        string? newPassword = _interaction.AskNewPassword("비밀번호 변경", "새 비밀번호를 입력하세요. 비밀번호는 최소 4자 이상이어야 합니다.");
        if (newPassword is null)
        {
            return;
        }

        _config.Password = _passwordService.CreatePasswordRecord(newPassword);
        _configStore.Save(_config);
        StatusMessage = "비밀번호를 변경했습니다.";
    }

    private async Task OpenRecoveryToolAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "이은성폴더잠금기 복구 도구를 실행하는 중입니다.";
            await _toolRunner.OpenRecoveryToolAsync().ConfigureAwait(true);
            _config = _configStore.Load();
            RefreshFolders(SelectedFolder?.Id);
            StatusMessage = "이은성폴더잠금기 복구 도구 실행이 끝났습니다.";
        }
        catch (Exception ex)
        {
            _interaction.ShowError(ex.Message);
            StatusMessage = "이은성폴더잠금기 복구 도구 실행 실패";
        }
        finally
        {
            IsBusy = false;
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
            StatusMessage = "UAC 승격을 요청하고 작업을 실행합니다.";
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
                StatusMessage = "작업이 완료되었습니다.";
            }
            else
            {
                StatusMessage = $"작업이 실패했습니다. 종료 코드: {exitCode}";
                _interaction.ShowError("작업이 실패했습니다. 로그와 상태를 확인하세요.");
            }
        }
        catch (Exception ex)
        {
            _config = _configStore.Load();
            RefreshFolders(folder.Id);
            _interaction.ShowError(ex.Message);
            StatusMessage = "작업 실패";
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
        StatusMessage = "취소를 요청했습니다. 이미 변경된 항목은 가능한 범위에서 원복합니다.";
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
        OperationPhase = "시작 중";
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
        OperationPhase = ToKoreanPhase(snapshot.Phase);
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

    private static string ToKoreanPhase(string phase)
    {
        return phase switch
        {
            "scan" => "항목 수 계산",
            "backup" => "ACL 백업",
            "lock" => "잠금 적용",
            "unlock" => "잠금 해제",
            "restore" => "복구",
            "rollback" => "원복",
            "starting" => "시작 중",
            _ => phase
        };
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
            return "빌드 후 release 폴더 또는 도구 프로젝트 출력 폴더";
        }
    }

    private void RaiseCommandStates()
    {
        RemoveFolderCommand.RaiseCanExecuteChanged();
        LockCommand.RaiseCanExecuteChanged();
        UnlockCommand.RaiseCanExecuteChanged();
        ChangePasswordCommand.RaiseCanExecuteChanged();
        OpenRecoveryToolCommand.RaiseCanExecuteChanged();
        CancelOperationCommand.RaiseCanExecuteChanged();
    }
}
