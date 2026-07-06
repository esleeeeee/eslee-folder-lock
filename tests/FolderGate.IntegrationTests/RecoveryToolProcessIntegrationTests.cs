using System.Diagnostics;
using System.Text;
using FolderGate.Core.Acl;
using FolderGate.Core.Models;
using FolderGate.Core.Storage;

namespace FolderGate.IntegrationTests;

[TestClass]
public sealed class RecoveryToolProcessIntegrationTests
{
    [TestMethod]
    [TestCategory("RequiresElevation")]
    public async Task RecoveryTool_Process_RestoresLockedFolderFromBackup()
    {
        string root = AclTestSupport.CreateTestRoot();
        TestTree tree = TestTree.Create(root);
        AppPaths paths = AppPaths.Resolve(root);
        ConfigStore configStore = new(paths);
        AclService service = new(paths);
        AclBackupStore backupStore = new(paths);
        RegisteredFolder folder = AclTestSupport.CreateFolder(tree.Target);
        string? backupPath = null;
        string recoveryToolPath = FindRecoveryToolExecutable();
        string processLogPath = Path.Combine(FindRepositoryRoot(), "tests", "FolderGate.IntegrationTests", "recovery-tool-process-test.log");
        int? processId = null;
        int? exitCode = null;
        string stdout = string.Empty;
        string stderr = string.Empty;
        bool recoverySucceeded = false;

        try
        {
            FolderGateConfig config = new();
            config.Folders.Add(folder);
            configStore.Save(config);

            AclOperationResult lockResult = await service.LockAsync(folder, LockMode.Hardened, CancellationToken.None);
            Assert.IsTrue(lockResult.Success, lockResult.Message);
            backupPath = lockResult.BackupPath;
            Assert.IsFalse(string.IsNullOrWhiteSpace(backupPath));

            folder.State = FolderLockState.Locked;
            folder.LatestBackupPath = backupPath;
            folder.LastOperationId = lockResult.OperationId;
            folder.LastOperationUtc = DateTimeOffset.UtcNow;
            folder.LastResult = "통합 테스트 잠금";
            configStore.Save(config);

            AclBackupFile backup = backupStore.Load(backupPath!);
            Dictionary<string, string> originalSddl = backup.Entries.ToDictionary(entry => entry.Path, entry => entry.Sddl, StringComparer.OrdinalIgnoreCase);
            AclTestSupport.AssertCommonOperationsDenied(tree);

            ProcessStartInfo startInfo = new()
            {
                FileName = recoveryToolPath,
                WorkingDirectory = root,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            startInfo.ArgumentList.Add("--root");
            startInfo.ArgumentList.Add(root);
            startInfo.ArgumentList.Add("--no-uac-relaunch");

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("FolderGate.RecoveryTool.exe 별도 프로세스를 시작하지 못했습니다.");

            processId = process.Id;
            await process.StandardInput.WriteLineAsync("1");
            await process.StandardInput.WriteLineAsync("1");
            await process.StandardInput.WriteLineAsync("RESTORE");
            process.StandardInput.Close();

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            stdout = await stdoutTask;
            stderr = await stderrTask;
            exitCode = process.ExitCode;

            Assert.AreEqual(0, process.ExitCode, $"RecoveryTool failed. stdout={stdout} stderr={stderr}");
            recoverySucceeded = true;

            Dictionary<string, string> restoredSddl = AclTestSupport.CaptureSddlMap(backup.Entries);
            CollectionAssert.AreEquivalent(originalSddl.Keys.ToList(), restoredSddl.Keys.ToList());
            foreach ((string path, string sddl) in originalSddl)
            {
                Assert.AreEqual(sddl, restoredSddl[path], $"RecoveryTool ACL SDDL mismatch: {path}");
            }

            AclTestSupport.AssertCommonOperationsAllowed(tree);
        }
        finally
        {
            WriteProcessLog(processLogPath, recoveryToolPath, processId, exitCode, tree.Target, backupPath, recoverySucceeded, stdout, stderr);
            await AclTestSupport.TryRestoreAsync(service, folder, backupPath);
            AclTestSupport.DeleteDirectory(root);
        }
    }

    private static string FindRecoveryToolExecutable()
    {
        string basePath = AppContext.BaseDirectory;
        string repoRoot = FindRepositoryRoot();
        string[] candidates =
        [
            Path.Combine(repoRoot, "release", "FolderGate.RecoveryTool.exe"),
            Path.Combine(basePath, "FolderGate.RecoveryTool.exe"),
            Path.Combine(repoRoot, "src", "FolderGate.RecoveryTool", "bin", "Debug", "net8.0-windows", "FolderGate.RecoveryTool.exe")
        ];

        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("FolderGate.RecoveryTool.exe를 release 또는 빌드 산출물에서 찾을 수 없습니다.");
    }

    private static string FindRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    private static void WriteProcessLog(
        string logPath,
        string recoveryToolPath,
        int? processId,
        int? exitCode,
        string targetPath,
        string? backupPath,
        bool success,
        string stdout,
        string stderr)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        File.WriteAllText(logPath,
            $"""
            TimestampUtc: {DateTimeOffset.UtcNow:O}
            RecoveryToolPath: {recoveryToolPath}
            ProcessId: {processId?.ToString() ?? "not-started"}
            ExitCode: {exitCode?.ToString() ?? "not-available"}
            TargetPath: {targetPath}
            BackupPath: {backupPath ?? "not-created"}
            Result: {(success ? "Success" : "Failed")}

            STDOUT:
            {stdout}

            STDERR:
            {stderr}
            """);

        Console.WriteLine(File.ReadAllText(logPath));
    }
}
