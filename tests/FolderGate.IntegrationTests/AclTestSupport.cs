using System.Security.AccessControl;
using System.Security.Principal;
using FolderGate.Core.Acl;
using FolderGate.Core.Models;
using FolderGate.Core.Storage;

namespace FolderGate.IntegrationTests;

internal static class AclTestSupport
{
    private const AccessControlSections SddlSections =
        AccessControlSections.Access |
        AccessControlSections.Owner |
        AccessControlSections.Group;

    public static string CreateTestRoot()
    {
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "TestRuns", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        return root;
    }

    public static RegisteredFolder CreateFolder(string target)
    {
        return new RegisteredFolder
        {
            Id = Guid.NewGuid().ToString("N"),
            DisplayName = "ACL 실제 동작 테스트",
            Path = target,
            OwnerSid = WindowsIdentity.GetCurrent().User?.Value ?? string.Empty,
            Mode = LockMode.Hardened
        };
    }

    public static Dictionary<string, string> CaptureSddlMap(IEnumerable<AclBackupEntry> entries)
    {
        return entries.ToDictionary(entry => entry.Path, entry => CaptureSddl(entry.Path, entry.IsDirectory), StringComparer.OrdinalIgnoreCase);
    }

    public static string CaptureSddl(string path, bool isDirectory)
    {
        FileSystemSecurity security = isDirectory
            ? new DirectoryInfo(path).GetAccessControl(SddlSections)
            : new FileInfo(path).GetAccessControl(SddlSections);

        return security.GetSecurityDescriptorSddlForm(SddlSections);
    }

    public static void AssertAccessDenied(string scenario, Action action)
    {
        try
        {
            action();
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        catch (IOException ex) when (IsAccessDenied(ex))
        {
            return;
        }
        catch (FileNotFoundException)
        {
            return;
        }
        catch (DirectoryNotFoundException)
        {
            return;
        }

        Assert.Fail($"{scenario} 작업이 Access Denied 없이 성공했습니다.");
    }

    public static void AssertCommonOperationsDenied(TestTree tree)
    {
        AssertAccessDenied("폴더 열기 및 목록 조회", () => Directory.EnumerateFileSystemEntries(tree.Target).ToList());
        AssertAccessDenied("파일 읽기", () => File.ReadAllText(tree.ReadFile));
        AssertAccessDenied("파일 쓰기", () => File.AppendAllText(tree.WriteFile, "blocked"));
        AssertAccessDenied("새 파일 생성", () => File.WriteAllText(Path.Combine(tree.Target, "new-file.txt"), "blocked"));
        AssertAccessDenied("새 하위 폴더 생성", () => Directory.CreateDirectory(Path.Combine(tree.Target, "new-folder")));
        AssertAccessDenied("파일 삭제", () => File.Delete(tree.DeleteFile));
        AssertAccessDenied("파일 이름 변경", () => File.Move(tree.RenameFile, Path.Combine(tree.Child, "renamed.txt")));
        AssertAccessDenied("외부 파일 복사", () => File.Copy(tree.ExternalFile, Path.Combine(tree.Target, "copied.txt")));
    }

    public static void AssertCommonOperationsAllowed(TestTree tree)
    {
        Assert.IsTrue(Directory.EnumerateFileSystemEntries(tree.Target).Any());

        Assert.AreEqual("read", File.ReadAllText(tree.ReadFile));
        File.AppendAllText(tree.WriteFile, "+write");
        StringAssert.Contains(File.ReadAllText(tree.WriteFile), "+write");

        string newFile = Path.Combine(tree.Target, "allowed-new-file.txt");
        File.WriteAllText(newFile, "created");
        Assert.AreEqual("created", File.ReadAllText(newFile));

        string newFolder = Path.Combine(tree.Target, "allowed-new-folder");
        Directory.CreateDirectory(newFolder);
        Assert.IsTrue(Directory.Exists(newFolder));

        string deleteFile = Path.Combine(tree.Child, "allowed-delete.txt");
        File.WriteAllText(deleteFile, "delete");
        File.Delete(deleteFile);
        Assert.IsFalse(File.Exists(deleteFile));

        string renameSource = Path.Combine(tree.Child, "allowed-rename.txt");
        string renameTarget = Path.Combine(tree.Child, "allowed-renamed.txt");
        File.WriteAllText(renameSource, "rename");
        File.Move(renameSource, renameTarget);
        Assert.IsTrue(File.Exists(renameTarget));

        string copied = Path.Combine(tree.Target, "allowed-copy.txt");
        File.Copy(tree.ExternalFile, copied, overwrite: true);
        Assert.AreEqual("outside", File.ReadAllText(copied));
    }

    public static async Task TryRestoreAsync(AclService service, RegisteredFolder folder, string? backupPath)
    {
        if (!string.IsNullOrWhiteSpace(backupPath) && File.Exists(backupPath))
        {
            await service.RestoreBackupAsync(folder.Id, backupPath, CancellationToken.None);
        }
    }

    public static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            RemoveReadOnlyAttributes(path);
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

    public static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private static bool IsAccessDenied(IOException ex)
    {
        return (ex.HResult & 0xFFFF) == 5;
    }

    private static void RemoveReadOnlyAttributes(string root)
    {
        foreach (string path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
        }
    }
}

internal sealed class TestTree
{
    public string Root { get; init; } = string.Empty;

    public string Target { get; init; } = string.Empty;

    public string Child { get; init; } = string.Empty;

    public string ReadFile { get; init; } = string.Empty;

    public string WriteFile { get; init; } = string.Empty;

    public string DeleteFile { get; init; } = string.Empty;

    public string RenameFile { get; init; } = string.Empty;

    public string ExternalFile { get; init; } = string.Empty;

    public static TestTree Create(string root)
    {
        string target = Path.Combine(root, "target 한글");
        string child = Path.Combine(target, "child folder");
        Directory.CreateDirectory(child);

        TestTree tree = new()
        {
            Root = root,
            Target = target,
            Child = child,
            ReadFile = Path.Combine(child, "read.txt"),
            WriteFile = Path.Combine(child, "write.txt"),
            DeleteFile = Path.Combine(child, "delete-me.txt"),
            RenameFile = Path.Combine(child, "rename-me.txt"),
            ExternalFile = Path.Combine(root, "outside.txt")
        };

        File.WriteAllText(tree.ReadFile, "read");
        File.WriteAllText(tree.WriteFile, "write");
        File.WriteAllText(tree.DeleteFile, "delete");
        File.WriteAllText(tree.RenameFile, "rename");
        File.WriteAllText(tree.ExternalFile, "outside");
        Directory.CreateDirectory(Path.Combine(child, "nested"));
        File.WriteAllText(Path.Combine(child, "nested", "nested-file.txt"), "nested");
        return tree;
    }
}

internal sealed class ThrowOnApplyIndexFaultInjector : IAclFaultInjector
{
    private readonly int _throwOnAttempt;

    public ThrowOnApplyIndexFaultInjector(int throwOnAttempt)
    {
        _throwOnAttempt = throwOnAttempt;
    }

    public void BeforeApplyDeny(AclBackupEntry entry, int attemptedIndex)
    {
        if (attemptedIndex == _throwOnAttempt)
        {
            throw new InvalidOperationException("테스트용 ACL 변경 실패");
        }
    }
}
