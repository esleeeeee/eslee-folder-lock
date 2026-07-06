using System.Diagnostics;
using FolderGate.Core.Acl;
using FolderGate.Core.Models;
using FolderGate.Core.Storage;

namespace FolderGate.IntegrationTests;

[TestClass]
public sealed class LargeHardenedModeIntegrationTests
{
    [TestMethod]
    public async Task HardenedLock_WithAtLeastTenThousandItems_CompletesAndLogsNoPerItemExternalProcess()
    {
        string root = AclTestSupport.CreateTestRoot();
        string target = Path.Combine(root, "large-target");
        Directory.CreateDirectory(target);
        AppPaths paths = AppPaths.Resolve(root);
        AclService service = new(paths);
        RegisteredFolder folder = AclTestSupport.CreateFolder(target);
        string? backupPath = null;

        try
        {
            CreateLargeTree(target, directories: 100, filesPerDirectory: 100);
            int itemCount = Directory.EnumerateFileSystemEntries(target, "*", SearchOption.AllDirectories).Count() + 1;
            Assert.IsTrue(itemCount >= 10_000, $"Expected at least 10,000 items, got {itemCount}.");

            Stopwatch stopwatch = Stopwatch.StartNew();
            AclOperationResult lockResult = await service.LockAsync(folder, LockMode.Hardened, CancellationToken.None);
            stopwatch.Stop();
            backupPath = lockResult.BackupPath;

            Assert.IsTrue(lockResult.Success, lockResult.Message);
            Assert.IsTrue(lockResult.ProcessedCount >= 10_000);

            AclOperationResult unlockResult = await service.UnlockAsync(folder, backupPath, CancellationToken.None);
            Assert.IsTrue(unlockResult.Success, unlockResult.Message);

            string logText = File.ReadAllText(paths.LogFilePath);
            StringAssert.Contains(logText, "perItemExternalProcessLaunches=0");
            Assert.IsFalse(logText.Contains("icacls", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(logText.Contains("powershell", StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(logText.Contains("cmd.exe", StringComparison.OrdinalIgnoreCase));

            Console.WriteLine($"Large hardened mode item count: {itemCount}");
            Console.WriteLine($"Large hardened mode lock elapsed: {stopwatch.Elapsed}");
        }
        finally
        {
            await AclTestSupport.TryRestoreAsync(service, folder, backupPath);
            AclTestSupport.DeleteDirectory(root);
        }
    }

    private static void CreateLargeTree(string target, int directories, int filesPerDirectory)
    {
        for (int directoryIndex = 0; directoryIndex < directories; directoryIndex++)
        {
            string directory = Path.Combine(target, $"d-{directoryIndex:D4}");
            Directory.CreateDirectory(directory);

            for (int fileIndex = 0; fileIndex < filesPerDirectory; fileIndex++)
            {
                File.WriteAllText(Path.Combine(directory, $"f-{fileIndex:D4}.txt"), string.Empty);
            }
        }
    }
}
