using System.Diagnostics;
using System.Text;
using FolderGate.Core.Acl;
using FolderGate.Core.Formatting;
using FolderGate.Core.Localization;
using FolderGate.Core.Models;
using FolderGate.Core.Security;
using FolderGate.Core.Storage;

Console.OutputEncoding = Encoding.UTF8;
Console.Title = AppText.RecoveryToolName;

try
{
    if (!WindowsElevation.IsCurrentProcessElevated())
    {
        if (HasArgument(args, "--no-uac-relaunch"))
        {
            Console.Error.WriteLine(AppText.RecoveryRequiresAdmin);
            return 740;
        }

        Console.WriteLine(AppText.RecoveryRequestingUac);
        RelaunchElevated(args);
        return 740;
    }

    AppPaths paths = AppPaths.Resolve(GetRootArgument(args));
    ConfigStore configStore = new(paths);
    FolderGateConfig config = configStore.Load();

    if (config.Folders.Count == 0)
    {
        Console.WriteLine(AppText.NoRegisteredTargets);
        return 0;
    }

    RegisteredFolder folder = SelectFolder(config.Folders);
    AclBackupStore backupStore = new(paths);
    IReadOnlyList<string> backups = backupStore.ListBackups(folder.Id);

    if (backups.Count == 0)
    {
        Console.WriteLine(AppText.NoAclBackupsForTarget);
        return 1;
    }

    string backupPath = SelectBackup(backups, backupStore);
    AclBackupFile backup = backupStore.Load(backupPath);

    Console.WriteLine();
    Console.WriteLine(AppText.PlannedRecoveryInfo);
    Console.WriteLine($"{AppText.TargetPath}: {backup.TargetPath}");
    Console.WriteLine($"{AppText.BackupFile}: {backupPath}");
    Console.WriteLine($"{AppText.BackupCreatedTime}: {LocalTimeFormatter.FormatLocal(backup.CreatedUtc)}");
    Console.WriteLine($"{AppText.RecoveryEntryCount}: {backup.Entries.Count}");
    Console.WriteLine(AppText.RestoreOnlyRecordedPaths);
    Console.Write(AppText.RestoreConfirmationPrompt);

    string? confirmation = Console.ReadLine();
    if (!string.Equals(confirmation, "RESTORE", StringComparison.Ordinal))
    {
        Console.WriteLine(AppText.RecoveryCanceled);
        return 0;
    }

    AclService aclService = new(paths);
    Progress<AclOperationProgress> progress = new(item =>
    {
        Console.WriteLine($"{item.Phase}: {item.Processed}/{item.Total} {item.CurrentPath}");
    });

    AclOperationResult result = aclService.RestoreBackupAsync(folder.Id, backupPath, CancellationToken.None, progress).GetAwaiter().GetResult();

    folder.State = result.Success ? FolderLockState.Unlocked : FolderLockState.RecoveryRequired;
    folder.LastOperationId = result.OperationId;
    folder.LastOperationUtc = DateTimeOffset.UtcNow;
    folder.LastResult = result.Message;
    configStore.Save(config);

    Console.WriteLine(result.Message);
    return result.Success ? 0 : 2;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static RegisteredFolder SelectFolder(IReadOnlyList<RegisteredFolder> folders)
{
    Console.WriteLine(AppText.SelectRecoveryTarget);
    for (int i = 0; i < folders.Count; i++)
    {
        RegisteredFolder folder = folders[i];
        Console.WriteLine($"{i + 1}. {folder.DisplayName} [{folder.State}]");
        Console.WriteLine($"   {folder.Path}");
    }

    int index = ReadIndex(folders.Count);
    return folders[index];
}

static string SelectBackup(IReadOnlyList<string> backups, AclBackupStore backupStore)
{
    Console.WriteLine();
    Console.WriteLine(AppText.SelectBackup);
    for (int i = 0; i < backups.Count; i++)
    {
        AclBackupFile backup = backupStore.Load(backups[i]);
        Console.WriteLine($"{i + 1}. {LocalTimeFormatter.FormatLocal(backup.CreatedUtc)} / {backup.Mode} / {backup.Entries.Count} {AppText.ItemsSuffix}");
        Console.WriteLine($"   {backups[i]}");
    }

    int index = ReadIndex(backups.Count);
    return backups[index];
}

static int ReadIndex(int count)
{
    while (true)
    {
        Console.Write(AppText.NumberPrompt);
        string? input = Console.ReadLine();
        if (int.TryParse(input, out int number) && number >= 1 && number <= count)
        {
            return number - 1;
        }

        Console.WriteLine(AppText.InvalidNumber);
    }
}

static string? GetRootArgument(string[] args)
{
    for (int i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], "--root", StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

static bool HasArgument(string[] args, string name)
{
    return args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));
}

static void RelaunchElevated(string[] args)
{
    string exePath = Environment.ProcessPath ?? throw new InvalidOperationException(AppText.CurrentExePathUnavailable);
    ProcessStartInfo startInfo = new()
    {
        FileName = exePath,
        UseShellExecute = true,
        Verb = "runas"
    };

    foreach (string arg in args)
    {
        startInfo.ArgumentList.Add(arg);
    }

    Process.Start(startInfo);
}
