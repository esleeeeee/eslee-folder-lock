using System.Security.Principal;
using FolderGate.Core.Acl;
using FolderGate.Core.Models;
using FolderGate.Core.Storage;

namespace FolderGate.IntegrationTests;

[TestClass]
public sealed class AclBackupIntegrationTests
{
    [TestMethod]
    public async Task BackupAndRestore_UsesOnlyTemporaryFoldersUnderTests()
    {
        string root = CreateTestRoot();
        string target = Path.Combine(root, "target 한글");
        string child = Path.Combine(target, "child folder");
        string file = Path.Combine(child, "large-name-sample.txt");

        try
        {
            Directory.CreateDirectory(child);
            await File.WriteAllTextAsync(file, "content must stay unchanged");

            AppPaths paths = AppPaths.Resolve(root);
            AclService service = new(paths);
            AclBackupStore backupStore = new(paths);
            RegisteredFolder folder = new()
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = "임시 대상",
                Path = target,
                OwnerSid = WindowsIdentity.GetCurrent().User?.Value ?? string.Empty,
                Mode = LockMode.Hardened
            };

            string operationId = Guid.NewGuid().ToString("N");
            AclBackupFile backup = await service.CreateBackupAsync(folder, LockMode.Hardened, operationId, CancellationToken.None);
            string backupPath = backupStore.CreateBackupPath(folder.Id, operationId);
            backupStore.Save(backupPath, backup);

            AclOperationResult result = await service.RestoreBackupAsync(folder.Id, backupPath, CancellationToken.None);

            Assert.IsTrue(result.Success, result.Message);
            Assert.IsTrue(backup.Entries.Count >= 3);
            Assert.AreEqual("content must stay unchanged", await File.ReadAllTextAsync(file));
            Assert.IsTrue(backup.Entries.All(entry => entry.Path.StartsWith(target, StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string CreateTestRoot()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestRuns", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        string? parent = Directory.GetParent(path)?.FullName;
        if (!string.IsNullOrWhiteSpace(parent) &&
            string.Equals(Path.GetFileName(parent), "TestRuns", StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(parent) &&
            !Directory.EnumerateFileSystemEntries(parent).Any())
        {
            Directory.Delete(parent);
        }
    }
}
