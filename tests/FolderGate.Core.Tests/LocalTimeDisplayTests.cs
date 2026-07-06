using FolderGate.Core.Acl;
using FolderGate.Core.Formatting;
using FolderGate.Core.Storage;

namespace FolderGate.Core.Tests;

[TestClass]
public sealed class LocalTimeDisplayTests
{
    [TestMethod]
    public void FormatLocal_ConvertsUtcToKoreanStandardTime()
    {
        DateTimeOffset utc = DateTimeOffset.Parse("2026-07-03T13:45:37+00:00");
        TimeZoneInfo kst = TimeZoneInfo.CreateCustomTimeZone("KST", TimeSpan.FromHours(9), "KST", "KST");

        string display = LocalTimeFormatter.FormatLocal(utc, kst, "KST");

        Assert.AreEqual("2026-07-03 22:45:37 (KST)", display);
    }

    [TestMethod]
    public void FormatLocal_UsesCurrentSystemLocalTimeZone()
    {
        DateTimeOffset utc = DateTimeOffset.Parse("2026-07-03T13:45:37+00:00");
        DateTimeOffset expectedLocal = TimeZoneInfo.ConvertTime(utc, TimeZoneInfo.Local);

        string display = LocalTimeFormatter.FormatLocal(utc);

        Assert.AreEqual($"{expectedLocal:yyyy-MM-dd HH:mm:ss} (Local)", display);
    }

    [TestMethod]
    public void OperationLogDisplayFormatter_ShowsLocalTimeInsteadOfRawUtcTimestamp()
    {
        TimeZoneInfo kst = TimeZoneInfo.CreateCustomTimeZone("KST", TimeSpan.FromHours(9), "KST", "KST");
        string jsonLines = """
            {"TimestampUtc":"2026-07-03T13:45:37+00:00","OperationId":"op","TargetId":"target","Operation":"lock","Path":"C:\\Temp\\target","Status":"Info","Message":"done"}
            """;

        string display = OperationLogDisplayFormatter.FormatJsonLinesForDisplay(jsonLines, kst, "KST");

        StringAssert.Contains(display, "2026-07-03 22:45:37 (KST)");
        Assert.IsFalse(display.Contains("2026-07-03T13:45:37", StringComparison.Ordinal));
        StringAssert.Contains(display, "lock");
        StringAssert.Contains(display, "done");
    }

    [TestMethod]
    public void BackupStore_LoadsLegacyTimestampUtcAndListsBackupsByActualCreatedUtc()
    {
        string root = CreateTestRoot();

        try
        {
            AppPaths paths = AppPaths.Resolve(root);
            AclBackupStore store = new(paths);
            string targetId = "target";
            string olderPath = store.CreateBackupPath(targetId, "older");
            string newerPath = store.CreateBackupPath(targetId, "newer");

            File.WriteAllText(olderPath, """
                {
                  "Version": 1,
                  "OperationId": "older",
                  "TargetId": "target",
                  "TargetPath": "C:\\Temp\\target",
                  "Mode": "Hardened",
                  "CreatedUtc": "2026-07-03T10:00:00+00:00",
                  "Entries": []
                }
                """);

            File.WriteAllText(newerPath, """
                {
                  "Version": 1,
                  "OperationId": "newer",
                  "TargetId": "target",
                  "TargetPath": "C:\\Temp\\target",
                  "Mode": "Hardened",
                  "TimestampUtc": "2026-07-03T13:45:37+00:00",
                  "Entries": []
                }
                """);

            File.SetLastWriteTimeUtc(olderPath, new DateTime(2026, 7, 4, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(newerPath, new DateTime(2026, 7, 3, 0, 0, 0, DateTimeKind.Utc));

            IReadOnlyList<string> backups = store.ListBackups(targetId);
            AclBackupFile loadedLegacy = store.Load(newerPath);

            Assert.AreEqual(newerPath, backups[0]);
            Assert.AreEqual(DateTimeOffset.Parse("2026-07-03T13:45:37+00:00"), loadedLegacy.CreatedUtc);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [TestMethod]
    public void BackupStore_Save_KeepsUtcCreatedTimestampInJson()
    {
        string root = CreateTestRoot();

        try
        {
            AppPaths paths = AppPaths.Resolve(root);
            AclBackupStore store = new(paths);
            string backupPath = store.CreateBackupPath("target", "op");

            store.Save(backupPath, new AclBackupFile
            {
                OperationId = "op",
                TargetId = "target",
                TargetPath = @"C:\Temp\target",
                Mode = FolderGate.Core.Models.LockMode.Hardened,
                CreatedUtc = DateTimeOffset.Parse("2026-07-03T13:45:37+00:00")
            });

            string json = File.ReadAllText(backupPath);

            StringAssert.Contains(json, "\"CreatedUtc\"");
            StringAssert.Contains(json, "2026-07-03T13:45:37+00:00");
            Assert.IsFalse(json.Contains("\"TimestampUtc\"", StringComparison.Ordinal));
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
