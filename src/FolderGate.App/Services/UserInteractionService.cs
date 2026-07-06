using System.IO;
using System.Windows;
using FolderGate.Core.Storage;
using Microsoft.Win32;

namespace FolderGate.App.Services;

public sealed class UserInteractionService : IUserInteractionService
{
    private readonly Window _owner;

    public UserInteractionService(Window owner)
    {
        _owner = owner;
    }

    public string? SelectFolder()
    {
        OpenFolderDialog dialog = new()
        {
            Title = "잠글 폴더 선택",
            Multiselect = false
        };

        return dialog.ShowDialog(_owner) == true ? dialog.FolderName : null;
    }

    public string? AskPassword(string title, string message)
    {
        PasswordDialog dialog = PasswordDialog.CreatePasswordPrompt(title, message);
        dialog.Owner = _owner;
        return dialog.ShowDialog() == true ? dialog.Password : null;
    }

    public string? AskNewPassword(string title, string message)
    {
        PasswordDialog dialog = PasswordDialog.CreateNewPasswordPrompt(title, message);
        dialog.Owner = _owner;
        return dialog.ShowDialog() == true ? dialog.Password : null;
    }

    public bool Confirm(string title, string message)
    {
        return MessageBox.Show(_owner, message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    public void ShowError(string message)
    {
        MessageBox.Show(_owner, message, "이은성폴더잠금기", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public void ShowInfo(string message)
    {
        MessageBox.Show(_owner, message, "이은성폴더잠금기", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowLogFile(string logFilePath)
    {
        string text = File.Exists(logFilePath)
            ? OperationLogDisplayFormatter.FormatJsonLinesForDisplay(File.ReadAllText(logFilePath))
            : "아직 기록된 로그가 없습니다.";

        LogWindow window = new(text)
        {
            Owner = _owner
        };
        window.ShowDialog();
    }
}
