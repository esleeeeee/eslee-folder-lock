using FolderGate.Core.Models;
using FolderGate.Core.Storage;

namespace FolderGate.Core.Tests;

[TestClass]
public sealed class ConfigStoreTests
{
    [TestMethod]
    public void SaveAndLoad_PreservesKoreanAndSpacePaths()
    {
        string root = CreateTestRoot();

        try
        {
            AppPaths paths = AppPaths.Resolve(root);
            ConfigStore store = new(paths);
            FolderGateConfig config = new();
            config.Folders.Add(new RegisteredFolder
            {
                DisplayName = "개인 자료",
                Path = Path.Combine(root, "한글 폴더", "개인 자료"),
                OwnerSid = "S-1-5-21-test"
            });

            store.Save(config);
            FolderGateConfig loaded = store.Load();

            Assert.AreEqual(1, loaded.Folders.Count);
            Assert.AreEqual("개인 자료", loaded.Folders[0].DisplayName);
            Assert.IsTrue(loaded.Folders[0].Path.Contains("한글 폴더", StringComparison.Ordinal));
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
