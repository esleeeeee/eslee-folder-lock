using System.Diagnostics;
using System.Text;
using FolderGate.Core.Acl;
using FolderGate.Core.Formatting;
using FolderGate.Core.Models;
using FolderGate.Core.Security;
using FolderGate.Core.Storage;

Console.OutputEncoding = Encoding.UTF8;
const string RecoveryToolDisplayName = "이은성폴더잠금기 복구 도구";
Console.Title = RecoveryToolDisplayName;

try
{
    if (!WindowsElevation.IsCurrentProcessElevated())
    {
        if (HasArgument(args, "--no-uac-relaunch"))
        {
            Console.Error.WriteLine($"{RecoveryToolDisplayName}는 관리자 권한이 필요합니다.");
            return 740;
        }

        Console.WriteLine($"{RecoveryToolDisplayName}는 관리자 권한이 필요합니다. UAC 승격을 요청합니다.");
        RelaunchElevated(args);
        return 740;
    }

    AppPaths paths = AppPaths.Resolve(GetRootArgument(args));
    ConfigStore configStore = new(paths);
    FolderGateConfig config = configStore.Load();

    if (config.Folders.Count == 0)
    {
        Console.WriteLine("등록된 잠금 대상이 없습니다.");
        return 0;
    }

    RegisteredFolder folder = SelectFolder(config.Folders);
    AclBackupStore backupStore = new(paths);
    IReadOnlyList<string> backups = backupStore.ListBackups(folder.Id);

    if (backups.Count == 0)
    {
        Console.WriteLine("선택한 대상의 ACL 백업 파일이 없습니다.");
        return 1;
    }

    string backupPath = SelectBackup(backups, backupStore);
    AclBackupFile backup = backupStore.Load(backupPath);

    Console.WriteLine();
    Console.WriteLine("복구 예정 정보");
    Console.WriteLine($"대상 경로: {backup.TargetPath}");
    Console.WriteLine($"백업 파일: {backupPath}");
    Console.WriteLine($"백업 생성 시간: {LocalTimeFormatter.FormatLocal(backup.CreatedUtc)}");
    Console.WriteLine($"복구 항목 수: {backup.Entries.Count}");
    Console.WriteLine("선택한 백업에 기록된 경로만 복구합니다.");
    Console.Write("복구를 실행하려면 RESTORE 를 입력하세요: ");

    string? confirmation = Console.ReadLine();
    if (!string.Equals(confirmation, "RESTORE", StringComparison.Ordinal))
    {
        Console.WriteLine("복구를 취소했습니다.");
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
    Console.WriteLine("복구할 대상을 선택하세요.");
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
    Console.WriteLine("사용할 백업을 선택하세요.");
    for (int i = 0; i < backups.Count; i++)
    {
        AclBackupFile backup = backupStore.Load(backups[i]);
        Console.WriteLine($"{i + 1}. {LocalTimeFormatter.FormatLocal(backup.CreatedUtc)} / {backup.Mode} / {backup.Entries.Count}개 항목");
        Console.WriteLine($"   {backups[i]}");
    }

    int index = ReadIndex(backups.Count);
    return backups[index];
}

static int ReadIndex(int count)
{
    while (true)
    {
        Console.Write("번호: ");
        string? input = Console.ReadLine();
        if (int.TryParse(input, out int number) && number >= 1 && number <= count)
        {
            return number - 1;
        }

        Console.WriteLine("올바른 번호를 입력하세요.");
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
    string exePath = Environment.ProcessPath ?? throw new InvalidOperationException("현재 실행 파일 경로를 확인할 수 없습니다.");
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
