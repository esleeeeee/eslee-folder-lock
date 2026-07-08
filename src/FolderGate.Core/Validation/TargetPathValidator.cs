using FolderGate.Core.Localization;
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
            return PathValidationResult.Invalid(AppText.InvalidPathFormat);
        }

        if (!Directory.Exists(fullPath))
        {
            return PathValidationResult.Invalid(AppText.ExistingFolderRequired);
        }

        string? root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            return PathValidationResult.Invalid(AppText.MissingDriveRoot);
        }

        if (WindowsPathComparer.AreSamePath(fullPath, root))
        {
            return PathValidationResult.Invalid(AppText.DriveRootBlocked);
        }

        DriveInfo drive;
        try
        {
            drive = new DriveInfo(root);
        }
        catch (ArgumentException)
        {
            return PathValidationResult.Invalid(AppText.DriveInfoUnavailable);
        }

        if (drive.DriveType != DriveType.Fixed)
        {
            return PathValidationResult.Invalid(AppText.FixedNtfsOnly);
        }

        try
        {
            if (!string.Equals(drive.DriveFormat, "NTFS", StringComparison.OrdinalIgnoreCase))
            {
                return PathValidationResult.Invalid(AppText.NtfsOnly);
            }
        }
        catch (IOException)
        {
            return PathValidationResult.Invalid(AppText.FileSystemInfoUnavailable);
        }
        catch (UnauthorizedAccessException)
        {
            return PathValidationResult.Invalid(AppText.FileSystemInfoAccessDenied);
        }

        string? protectedReason = GetProtectedPathReason(fullPath);
        if (protectedReason is not null)
        {
            return PathValidationResult.Invalid(protectedReason);
        }

        List<string> warnings = [];
        if (ContainsReparsePoint(fullPath))
        {
            warnings.Add(AppText.ReparsePointWarning);
        }

        return PathValidationResult.Valid(fullPath, warnings);
    }

    private string? GetProtectedPathReason(string fullPath)
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(windows) && WindowsPathComparer.IsSameOrChild(fullPath, windows))
        {
            return AppText.WindowsFolderBlocked;
        }

        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrWhiteSpace(programFiles) && WindowsPathComparer.IsSameOrChild(fullPath, programFiles))
        {
            return AppText.ProgramFilesBlocked;
        }

        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrWhiteSpace(programFilesX86) && WindowsPathComparer.IsSameOrChild(fullPath, programFilesX86))
        {
            return AppText.ProgramFilesX86Blocked;
        }

        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrWhiteSpace(programData) && WindowsPathComparer.IsSameOrChild(fullPath, programData))
        {
            return AppText.ProgramDataBlocked;
        }

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile) && WindowsPathComparer.AreSamePath(fullPath, userProfile))
        {
            return AppText.UserProfileRootBlocked;
        }

        foreach (string? oneDriveRoot in GetOneDriveRoots())
        {
            if (!string.IsNullOrWhiteSpace(oneDriveRoot) && Directory.Exists(oneDriveRoot) && WindowsPathComparer.AreSamePath(fullPath, oneDriveRoot))
            {
                return AppText.OneDriveRootBlocked;
            }
        }

        if (WindowsPathComparer.IsSameOrChild(fullPath, _paths.ProjectRoot))
        {
            return AppText.ProjectFolderBlocked;
        }

        string? parent = Directory.GetParent(_paths.ProjectRoot)?.FullName;
        while (!string.IsNullOrWhiteSpace(parent))
        {
            if (WindowsPathComparer.AreSamePath(fullPath, parent))
            {
                return AppText.ProjectParentBlocked;
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
