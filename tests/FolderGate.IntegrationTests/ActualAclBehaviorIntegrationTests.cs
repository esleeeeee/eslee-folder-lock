using FolderGate.Core.Acl;
using FolderGate.Core.Models;
using FolderGate.Core.Storage;

namespace FolderGate.IntegrationTests;

[TestClass]
public sealed class ActualAclBehaviorIntegrationTests
{
    [TestMethod]
    public async Task HardenedLock_BlocksCommonOperations_ThenUnlockRestoresAclAndOperations()
    {
        string root = AclTestSupport.CreateTestRoot();
        TestTree tree = TestTree.Create(root);
        AppPaths paths = AppPaths.Resolve(root);
        AclService service = new(paths);
        RegisteredFolder folder = AclTestSupport.CreateFolder(tree.Target);
        string? backupPath = null;

        try
        {
            AclOperationResult lockResult = await service.LockAsync(folder, LockMode.Hardened, CancellationToken.None);
            Assert.IsTrue(lockResult.Success, lockResult.Message);
            backupPath = lockResult.BackupPath;
            Assert.IsFalse(string.IsNullOrWhiteSpace(backupPath));

            AclBackupFile backup = new AclBackupStore(paths).Load(backupPath!);
            Dictionary<string, string> originalSddl = backup.Entries.ToDictionary(entry => entry.Path, entry => entry.Sddl, StringComparer.OrdinalIgnoreCase);

            AclTestSupport.AssertCommonOperationsDenied(tree);

            AclOperationResult unlockResult = await service.UnlockAsync(folder, backupPath, CancellationToken.None);
            Assert.IsTrue(unlockResult.Success, unlockResult.Message);

            Dictionary<string, string> restoredSddl = AclTestSupport.CaptureSddlMap(backup.Entries);
            CollectionAssert.AreEquivalent(originalSddl.Keys.ToList(), restoredSddl.Keys.ToList());
            foreach ((string path, string sddl) in originalSddl)
            {
                Assert.AreEqual(sddl, restoredSddl[path], $"ACL SDDL mismatch after unlock: {path}");
            }

            AclTestSupport.AssertCommonOperationsAllowed(tree);
        }
        finally
        {
            await AclTestSupport.TryRestoreAsync(service, folder, backupPath);
            AclTestSupport.DeleteDirectory(root);
        }
    }
}
