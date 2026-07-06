using System.Windows;
using FolderGate.App.Services;
using FolderGate.App.ViewModels;
using FolderGate.Core.Storage;

namespace FolderGate.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AppPaths paths = AppPaths.Resolve();
        ToolLocator toolLocator = new(paths);
        DataContext = new MainViewModel(paths, new UserInteractionService(this), new ElevatedToolRunner(paths, toolLocator));
    }
}
