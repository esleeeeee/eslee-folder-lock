using System.Windows;

namespace FolderGate.App;

public partial class LogWindow : Window
{
    public LogWindow(string logText)
    {
        InitializeComponent();
        LogText.Text = logText;
    }
}
