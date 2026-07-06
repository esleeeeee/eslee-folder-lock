using FolderGate.Core.Acl;
using FolderGate.Core.Models;
using FolderGate.Core.Storage;

namespace FolderGate.IntegrationTests;

[TestClass]
public sealed class CancelAndFailureIntegrationTests
{
    [TestMethod]
    public async Task HardenedLock_Cancel_RollsBackChangedItemsInReverseOrder()
    {
        string root = AclTestSupport.CreateTestRoot();
        TestTree tree = TestTree.Create(root);
        AppPaths paths = AppPaths.Resolve(root);
        AclService service = new(paths);
        RegisteredFolder folder = AclTestSupport.CreateFolder(tree.Target);
        string? backupPath = null;
        using CancellationTokenSource cts = new();
        List<string> lockedPaths = [];
        List<string> rollbackPaths = [];

        Progress<AclOperationProgress> progress = new(item =>
        {
            if (item.Phase == "lock" && item.CurrentPath is not null && item.Processed > lockedPaths.Count)
            {
                lockedPaths.Add(item.CurrentPath);
                if (item.Processed == 3)
                {
                    cts.Cancel();
                }
            }

            if (item.Phase == "rollback" && item.CurrentPath is not null && item.Processed > rollbackPaths.Count)
            {
                rollbackPaths.Add(item.CurrentPath);
            }
        });

        try
        {
            AclOperationResult result = await service.LockAsync(folder, LockMode.Hardened, cts.Token, progress);
            backupPath = result.BackupPath;

            Assert.IsFalse(result.Success);
            Assert.IsFalse(result.RecoveryRequired, result.Message);
            Assert.IsTrue(lockedPaths.Count >= 3);
            CollectionAssert.AreEqual(lockedPaths.AsEnumerable().Reverse().ToList(), rollbackPaths);
            AclTestSupport.AssertCommonOperationsAllowed(tree);
        }
        finally
        {
            await AclTestSupport.TryRestoreAsync(service, folder, backupPath);
            AclTestSupport.DeleteDirectory(root);
        }
    }

    [TestMethod]
    public async Task HardenedLock_WhenAclChangeFails_RecordsFailureAndRollsBackChangedItems()
    {
        string root = AclTestSupport.CreateTestRoot();
        TestTree tree = TestTree.Create(root);
        AppPaths paths = AppPaths.Resolve(root);
        AclService service = new(new AclBackupStore(paths), new JsonOperationLogger(paths), new ThrowOnApplyIndexFaultInjector(throwOnAttempt: 4));
        RegisteredFolder folder = AclTestSupport.CreateFolder(tree.Target);
        string? backupPath = null;

        try
        {
            AclOperationResult result = await service.LockAsync(folder, LockMode.Hardened, CancellationToken.None);
            backupPath = result.BackupPath;

            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.FailedCount);
            Assert.IsFalse(result.RecoveryRequired, result.Message);
            AclTestSupport.AssertCommonOperationsAllowed(tree);
        }
        finally
        {
            await AclTestSupport.TryRestoreAsync(service, folder, backupPath);
            AclTestSupport.DeleteDirectory(root);
        }
    }
}
