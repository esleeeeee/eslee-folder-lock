using System.Diagnostics;
using FolderGate.Core.Acl;
using FolderGate.Core.Models;
using FolderGate.Core.Storage;

namespace FolderGate.IntegrationTests;

[TestClass]
public sealed class ElevatedHelperRestoreIntegrationTests
{
    [TestMethod]
    public async Task ElevatedHelper_CanRestoreWhenRunningFromElevatedTestProcess()
    {
        if (!AclTestSupport.IsAdministrator())
        {
            Console.WriteLine("현재 테스트 프로세스가 관리자 권한이 아니므로 ElevatedHelper 관리자 복구 검증은 실행하지 않았습니다.");
            return;
        }

        string root = AclTestSupport.CreateTestRoot();
        TestTree tree = TestTree.Create(root);
        AppPaths paths = AppPaths.Resolve(root);
        ConfigStore configStore = new(paths);
        AclService service = new(paths);
        RegisteredFolder folder = AclTestSupport.CreateFolder(tree.Target);
        string? backupPath = null;

        try
        {
            FolderGateConfig config = new();
            config.Folders.Add(folder);
            configStore.Save(config);

            AclOperationResult lockResult = await service.LockAsync(folder, LockMode.Hardened, CancellationToken.None);
            Assert.IsTrue(lockResult.Success, lockResult.Message);
            backupPath = lockResult.BackupPath;

            string helperPath = FindHelperExecutable();
            ProcessStartInfo startInfo = new()
            {
                FileName = helperPath,
                UseShellExecute = false,
                WorkingDirectory = root
            };
            startInfo.ArgumentList.Add("restore");
            startInfo.ArgumentList.Add("--root");
            startInfo.ArgumentList.Add(root);
            startInfo.ArgumentList.Add("--target-id");
            startInfo.ArgumentList.Add(folder.Id);
            startInfo.ArgumentList.Add("--backup");
            startInfo.ArgumentList.Add(backupPath!);
            startInfo.ArgumentList.Add("--operation-id");
            startInfo.ArgumentList.Add(Guid.NewGuid().ToString("N"));

            using Process process = Process.Start(startInfo) ?? throw new InvalidOperationException("ElevatedHelper를 시작하지 못했습니다.");
            await process.WaitForExitAsync();
            Assert.AreEqual(0, process.ExitCode);
            AclTestSupport.AssertCommonOperationsAllowed(tree);
        }
        finally
        {
            await AclTestSupport.TryRestoreAsync(service, folder, backupPath);
            AclTestSupport.DeleteDirectory(root);
        }
    }

    private static string FindHelperExecutable()
    {
        string basePath = AppContext.BaseDirectory;
        string[] candidates =
        [
            Path.Combine(basePath, "FolderGate.ElevatedHelper.exe"),
            Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", "..", "..", "src", "FolderGate.ElevatedHelper", "bin", "Debug", "net8.0-windows", "FolderGate.ElevatedHelper.exe"))
        ];

        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("FolderGate.ElevatedHelper.exe를 찾을 수 없습니다.");
    }
}
