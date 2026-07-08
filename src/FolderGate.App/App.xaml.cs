using System.Windows;
using FolderGate.App.Services;
using FolderGate.Core.Localization;
using FolderGate.Core.Storage;

namespace FolderGate.App;

public partial class App : System.Windows.Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        AppStartupArguments arguments;
        try
        {
            arguments = AppStartupArguments.Parse(e.Args);
        }
        catch (ArgumentException ex)
        {
            System.Windows.MessageBox.Show(ex.Message, AppText.ProductName, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(2);
            return;
        }

        AppPaths paths = AppPaths.Resolve(arguments.RootPath);
        TryMigrateExplorerContextMenu(paths);
        if (arguments.ResumeTemporaryUnlocks)
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            int resumeExitCode = await new TemporaryUnlockResumeRunner(paths).RunAsync().ConfigureAwait(true);
            Shutdown(resumeExitCode);
            return;
        }

        if (arguments.UnlockPath is null)
        {
            MainWindow = new MainWindow(paths);
            MainWindow.Show();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        int exitCode = await new UnlockPromptRunner(paths).RunAsync(arguments.UnlockPath).ConfigureAwait(true);
        Shutdown(exitCode);
    }

    private static void TryMigrateExplorerContextMenu(AppPaths paths)
    {
        try
        {
            ToolLocator toolLocator = new(paths);
            new ExplorerContextMenuService(paths, toolLocator).MigrateLegacyInstallIfPresent();
        }
        catch
        {
            // Best-effort migration only. Normal app startup and unlock flow should continue.
        }
    }
}
