namespace FolderGate.App.Services;

public interface IUserInteractionService
{
    string? SelectFolder();

    string? AskPassword(string title, string message);

    string? AskNewPassword(string title, string message);

    bool Confirm(string title, string message);

    void ShowError(string message);

    void ShowInfo(string message);

    void ShowLogFile(string logFilePath);
}
