using Microsoft.Win32;
using FolderGate.Core.Localization;
using FolderGate.Core.Storage;

namespace FolderGate.App.Services;

public sealed class StartupRelockService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "eslee-folder-locker-temporary-relock";
    private const string LegacyRunValueName = "eslee-folder-lock-temporary-relock";

    private readonly AppPaths _paths;
    private readonly ToolLocator _toolLocator;

    public StartupRelockService(AppPaths paths, ToolLocator toolLocator)
    {
        _paths = paths;
        _toolLocator = toolLocator;
    }

    public void Install()
    {
        string appPath = _toolLocator.FindExecutable("FolderGate.App");
        using RegistryKey runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException(AppText.StartupRegistryKeyCreateFailed);
        runKey.SetValue(RunValueName, BuildCommand(appPath, _paths.ProjectRoot));
        runKey.DeleteValue(LegacyRunValueName, throwOnMissingValue: false);
    }

    public void Uninstall()
    {
        using RegistryKey? runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        runKey?.DeleteValue(RunValueName, throwOnMissingValue: false);
        runKey?.DeleteValue(LegacyRunValueName, throwOnMissingValue: false);
    }

    public static string BuildCommand(string appPath, string projectRoot)
    {
        return $"\"{appPath}\" --resume-temporary-unlocks --root \"{projectRoot}\"";
    }
}
