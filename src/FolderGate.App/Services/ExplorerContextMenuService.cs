using Microsoft.Win32;
using FolderGate.Core.Localization;
using FolderGate.Core.Storage;

namespace FolderGate.App.Services;

public sealed class ExplorerContextMenuService
{
    private static readonly string[] MenuKeyPaths =
    [
        @"Software\Classes\Directory\shell\eslee-folder-locker-unlock",
        @"Software\Classes\Folder\shell\eslee-folder-locker-unlock"
    ];

    private static readonly string[] LegacyMenuKeyPaths =
    [
        @"Software\Classes\Directory\shell\eslee-folder-lock-unlock",
        @"Software\Classes\Folder\shell\eslee-folder-lock-unlock"
    ];

    private readonly AppPaths _paths;
    private readonly ToolLocator _toolLocator;

    public ExplorerContextMenuService(AppPaths paths, ToolLocator toolLocator)
    {
        _paths = paths;
        _toolLocator = toolLocator;
    }

    public void Install()
    {
        string appPath = _toolLocator.FindExecutable("FolderGate.App");
        foreach (string menuKeyPath in MenuKeyPaths)
        {
            using RegistryKey menuKey = Registry.CurrentUser.CreateSubKey(menuKeyPath, writable: true)
                ?? throw new InvalidOperationException(AppText.ExplorerMenuKeyCreateFailed);
            menuKey.SetValue(null, AppText.ExplorerUnlockMenuText);
            menuKey.SetValue("Icon", appPath);

            using RegistryKey commandKey = Registry.CurrentUser.CreateSubKey(menuKeyPath + @"\command", writable: true)
                ?? throw new InvalidOperationException(AppText.ExplorerMenuCommandKeyCreateFailed);
            commandKey.SetValue(null, BuildCommand(appPath, _paths.ProjectRoot));
        }

        RemoveKeys(LegacyMenuKeyPaths);
    }

    public void MigrateLegacyInstallIfPresent()
    {
        if (LegacyMenuKeyPaths.Concat(MenuKeyPaths).Any(KeyExists))
        {
            Install();
        }
    }

    public void Uninstall()
    {
        RemoveKeys(MenuKeyPaths);
        RemoveKeys(LegacyMenuKeyPaths);
    }

    private static bool KeyExists(string menuKeyPath)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(menuKeyPath);
        return key is not null;
    }

    private static void RemoveKeys(IEnumerable<string> menuKeyPaths)
    {
        foreach (string menuKeyPath in menuKeyPaths)
        {
            Registry.CurrentUser.DeleteSubKeyTree(menuKeyPath, throwOnMissingSubKey: false);
        }
    }

    public static string BuildCommand(string appPath, string projectRoot)
    {
        return $"\"{appPath}\" --unlock-path \"%1\" --root \"{projectRoot}\"";
    }
}
