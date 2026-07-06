using FolderGate.Core.Storage;

namespace FolderGate.Core.Validation;

public sealed class TargetPathValidator
{
    private const int ReparseScanLimit = 5_000;
    private readonly AppPaths _paths;

    public TargetPathValidator(AppPaths paths)
    {
        _paths = paths;
    }

    public PathValidationResult ValidateDirectory(string rawPath)
    {
        string fullPath;
        try
        {
            fullPath = WindowsPathComparer.Normalize(rawPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return PathValidationResult.Invalid("경로 형식이 올바르지 않습니다.");
        }

        if (!Directory.Exists(fullPath))
        {
            return PathValidationResult.Invalid("존재하는 폴더만 등록할 수 있습니다.");
        }

        string? root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            return PathValidationResult.Invalid("드라이브 루트가 없는 경로는 등록할 수 없습니다.");
        }

        if (WindowsPathComparer.AreSamePath(fullPath, root))
        {
            return PathValidationResult.Invalid("C:\\ 또는 D:\\ 같은 드라이브 루트 전체는 잠글 수 없습니다.");
        }

        DriveInfo drive;
        try
        {
            drive = new DriveInfo(root);
        }
        catch (ArgumentException)
        {
            return PathValidationResult.Invalid("드라이브 정보를 확인할 수 없습니다.");
        }

        if (drive.DriveType != DriveType.Fixed)
        {
            return PathValidationResult.Invalid("로컬 고정 드라이브의 NTFS 폴더만 등록할 수 있습니다.");
        }

        try
        {
            if (!string.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
            {
                return PathValidationResult.Invalid("NTFS 파일 시스템 폴더만 등록할 수 있습니다.");
            }
        }
        catch (IOException)
        {
            return PathValidationResult.Invalid("파일 시스템 정보를 확인할 수 없습니다.");
        }
        catch (UnauthorizedAccessException)
        {
            return PathValidationResult.Invalid("파일 시스템 정보를 확인할 권한이 없습니다.");
        }

        string? protectedReason = GetProtectedPathReason(fullPath);
        if (protectedReason is not null)
        {
            return PathValidationResult.Invalid(protectedReason);
        }

        List<string> warnings = [];
        if (ContainsReparsePoint(fullPath))
        {
            warnings.Add("이 폴더 안에 junction, symbolic link 또는 reparse point가 있습니다. 강화 모드에서는 해당 항목을 따라가지 않고 건너뜁니다.");
        }

        return PathValidationResult.Valid(fullPath, warnings);
    }

    private string? GetProtectedPathReason(string fullPath)
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(windows) && WindowsPathComparer.IsSameOrChild(fullPath, windows))
        {
            return "Windows 시스템 폴더 또는 그 하위 폴더는 등록할 수 없습니다.";
        }

        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles) && WindowsPathComparer.IsSameOrChild(fullPath, programFiles))
        {
            return "Program Files 폴더 또는 그 하위 폴더는 등록할 수 없습니다.";
        }

        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86) && WindowsPathComparer.IsSameOrChild(fullPath, programFilesX86))
        {
            return "Program Files (x86) 폴더 또는 그 하위 폴더는 등록할 수 없습니다.";
        }

        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrWhiteSpace(programData) && WindowsPathComparer.IsSameOrChild(fullPath, programData))
        {
            return "ProgramData 폴더 또는 그 하위 폴더는 등록할 수 없습니다.";
        }

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile) && WindowsPathComparer.AreSamePath(fullPath, userProfile))
        {
            return "사용자 프로필 루트는 등록할 수 없습니다. Documents 같은 하위 폴더를 선택하세요.";
        }

        foreach (string? oneDriveRoot in GetOneDriveRoots())
        {
            if (!string.IsNullOrWhiteSpace(oneDriveRoot) && Directory.Exists(oneDriveRoot) && WindowsPathComparer.AreSamePath(fullPath, oneDriveRoot))
            {
                return "OneDrive 루트 자체는 등록할 수 없습니다. 그 아래의 특정 하위 폴더를 선택하세요.";
            }
        }

        if (WindowsPathComparer.IsSameOrChild(fullPath, _paths.ProjectRoot))
        {
            return "이은성폴더잠금기 프로젝트 폴더와 그 하위 폴더는 잠금 대상으로 사용할 수 없습니다.";
        }

        string? parent = Directory.GetParent(_paths.ProjectRoot)?.FullName;
        while (!string.IsNullOrWhiteSpace(parent))
        {
            if (WindowsPathComparer.AreSamePath(fullPath, parent))
            {
                return "이은성폴더잠금기 프로젝트 루트의 상위 폴더는 잠금 대상으로 사용할 수 없습니다.";
            }

            parent = Directory.GetParent(parent)?.FullName;
        }

        return null;
    }

    private static IEnumerable<string?> GetOneDriveRoots()
    {
        yield return Environment.GetEnvironmentVariable("OneDrive");
        yield return Environment.GetEnvironmentVariable("OneDriveCommercial");
        yield return Environment.GetEnvironmentVariable("OneDriveConsumer");
    }

    private static bool ContainsReparsePoint(string root)
    {
        int seen = 0;
        Stack<string> pending = new();
        pending.Push(root);

        while (pending.Count > 0 && seen < ReparseScanLimit)
        {
            string current = pending.Pop();
            seen++;

            DirectoryInfo directory;
            try
            {
                directory = new DirectoryInfo(current);
                if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    return true;
                }

                foreach (FileSystemInfo entry in directory.EnumerateFileSystemInfos())
                {
                    if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        return true;
                    }

                    if (entry is DirectoryInfo childDirectory)
                    {
                        pending.Push(childDirectory.FullName);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            catch (IOException)
            {
                continue;
            }
        }

        return false;
    }
}
