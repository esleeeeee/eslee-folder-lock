using FolderGate.Core.Formatting;
using FolderGate.Core.Models;

namespace FolderGate.App.ViewModels;

public sealed class FolderItemViewModel
{
    public FolderItemViewModel(RegisteredFolder model)
    {
        Model = model;
    }

    public RegisteredFolder Model { get; }

    public string Id => Model.Id;

    public string DisplayName => Model.DisplayName;

    public string Path => Model.Path;

    public string ModeText => Model.Mode == LockMode.Quick ? "빠른 모드" : "강화 모드";

    public string StateText => Model.State switch
    {
        FolderLockState.Locked => "잠김",
        FolderLockState.Working => "작업 중",
        FolderLockState.RecoveryRequired => "복구 필요",
        _ => "해제됨"
    };

    public string LastOperationText => Model.LastOperationUtc is null
        ? "-"
        : LocalTimeFormatter.FormatLocal(Model.LastOperationUtc.Value);

    public string LastResult => string.IsNullOrWhiteSpace(Model.LastResult) ? "-" : Model.LastResult;

    public string WarningText => Model.HasReparsePointWarning ? "재분석 지점 있음" : string.Empty;
}
