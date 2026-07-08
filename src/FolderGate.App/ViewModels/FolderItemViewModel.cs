using FolderGate.Core.Formatting;
using FolderGate.Core.Localization;
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

    public string ModeText => AppText.ModeName(Model.Mode);

    public string StateText => AppText.StateName(Model.State);

    public string LastOperationText => Model.LastOperationUtc is null
        ? "-"
        : LocalTimeFormatter.FormatLocal(Model.LastOperationUtc.Value);

    public string LastResult => string.IsNullOrWhiteSpace(Model.LastResult) ? "-" : Model.LastResult;

    public string WarningText => Model.HasReparsePointWarning ? AppText.ReparsePointShort : string.Empty;
}
