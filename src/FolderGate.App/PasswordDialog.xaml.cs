using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using FolderGate.Core.Localization;

namespace FolderGate.App;

public partial class PasswordDialog : Window
{
    private readonly bool _requiresConfirmation;

    private PasswordDialog(string title, string message, bool requiresConfirmation, bool includesUnlockDuration)
    {
        InitializeComponent();
        Title = title;
        MessageText.Text = message;
        _requiresConfirmation = requiresConfirmation;
        ConfirmPanel.Visibility = requiresConfirmation ? Visibility.Visible : Visibility.Collapsed;
        UnlockDurationPanel.Visibility = includesUnlockDuration ? Visibility.Visible : Visibility.Collapsed;
        UnlockDurationInput.ItemsSource = UnlockDurationOption.Defaults;
        UnlockDurationInput.SelectedIndex = 0;
    }

    public string Password => PasswordInput.Password;

    public TimeSpan? SelectedUnlockDuration => (UnlockDurationInput.SelectedItem as UnlockDurationOption)?.Duration;

    public static PasswordDialog CreatePasswordPrompt(string title, string message)
    {
        return new PasswordDialog(title, message, requiresConfirmation: false, includesUnlockDuration: false);
    }

    public static PasswordDialog CreateUnlockPasswordPrompt(string title, string message)
    {
        return new PasswordDialog(title, message, requiresConfirmation: false, includesUnlockDuration: true);
    }

    public static PasswordDialog CreateNewPasswordPrompt(string title, string message)
    {
        return new PasswordDialog(title, message, requiresConfirmation: true, includesUnlockDuration: false);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        string? validationError = PasswordDialogValidator.Validate(PasswordInput.Password, ConfirmInput.Password, _requiresConfirmation);
        if (validationError is not null)
        {
            System.Windows.MessageBox.Show(this, validationError, AppText.ProductName, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
    }

    private void PasswordDialog_Loaded(object sender, RoutedEventArgs e)
    {
        if (Owner is not null)
        {
            CenterToOwner();
            return;
        }

        CenterToCurrentScreen();
    }

    private void CenterToOwner()
    {
        Window owner = Owner;
        double width = ActualWidth > 0 ? ActualWidth : Width;
        double height = ActualHeight > 0 ? ActualHeight : MinHeight;
        Left = owner.Left + Math.Max(0, (owner.ActualWidth - width) / 2);
        Top = owner.Top + Math.Max(0, (owner.ActualHeight - height) / 2);
    }

    private void CenterToCurrentScreen()
    {
        Screen screen = Screen.FromPoint(Control.MousePosition);
        double dpiScaleX = 1.0;
        double dpiScaleY = 1.0;

        PresentationSource? source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is not null)
        {
            dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
            dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
        }

        double width = ActualWidth > 0 ? ActualWidth : Width;
        double height = ActualHeight > 0 ? ActualHeight : MinHeight;
        double workLeft = screen.WorkingArea.Left / dpiScaleX;
        double workTop = screen.WorkingArea.Top / dpiScaleY;
        double workWidth = screen.WorkingArea.Width / dpiScaleX;
        double workHeight = screen.WorkingArea.Height / dpiScaleY;

        Left = workLeft + Math.Max(0, (workWidth - width) / 2);
        Top = workTop + Math.Max(0, (workHeight - height) / 2);
    }
}

public sealed class UnlockDurationOption
{
    private UnlockDurationOption(string label, TimeSpan? duration)
    {
        Label = label;
        Duration = duration;
    }

    public string Label { get; }

    public TimeSpan? Duration { get; }

    public static IReadOnlyList<UnlockDurationOption> Defaults { get; } =
    [
        new(AppText.LanguageCode == "en" ? "1 minute" : "1분", TimeSpan.FromMinutes(1)),
        new(AppText.LanguageCode == "en" ? "5 minutes" : "5분", TimeSpan.FromMinutes(5)),
        new(AppText.LanguageCode == "en" ? "10 minutes" : "10분", TimeSpan.FromMinutes(10)),
        new(AppText.LanguageCode == "en" ? "30 minutes" : "30분", TimeSpan.FromMinutes(30)),
        new(AppText.LanguageCode == "en" ? "1 hour" : "1시간", TimeSpan.FromHours(1)),
        new(AppText.LanguageCode == "en" ? "1 day" : "하루", TimeSpan.FromDays(1)),
        new(AppText.LanguageCode == "en" ? "Unlock permanently" : "완전 해제", null)
    ];
}
