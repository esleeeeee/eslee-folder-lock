using System.Windows;

namespace FolderGate.App;

public partial class PasswordDialog : Window
{
    private readonly bool _requiresConfirmation;

    private PasswordDialog(string title, string message, bool requiresConfirmation)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        _requiresConfirmation = requiresConfirmation;
        ConfirmPanel.Visibility = requiresConfirmation ? Visibility.Visible : Visibility.Collapsed;
    }

    public string Password => PasswordInput.Password;

    public static PasswordDialog CreatePasswordPrompt(string title, string message)
    {
        return new PasswordDialog(title, message, requiresConfirmation: false);
    }

    public static PasswordDialog CreateNewPasswordPrompt(string title, string message)
    {
        return new PasswordDialog(title, message, requiresConfirmation: true);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        string? validationError = PasswordDialogValidator.Validate(PasswordInput.Password, ConfirmInput.Password, _requiresConfirmation);
        if (validationError is not null)
        {
            MessageBox.Show(this, validationError, "이은성폴더잠금기", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }
}
