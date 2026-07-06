namespace FolderGate.Core.Acl;

internal static class FileSystemTargetEnumerator
{
    public static IReadOnlyList<AclBackupEntry> CreateEmptyEntries(
        string root,
        bool recursive,
        CancellationToken cancellationToken = default,
        IProgress<AclOperationProgress>? progress = null)
    {
        List<AclBackupEntry> entries = [];

        foreach ((string path, bool isDirectory) in Enumerate(root, recursive))
        {
            cancellationToken.ThrowIfCancellationRequested();
            entries.Add(new AclBackupEntry
            {
                Path = path,
                IsDirectory = isDirectory
            });
            progress?.Report(new AclOperationProgress
            {
                Phase = "scan",
                Processed = entries.Count,
                Total = 0,
                CurrentPath = path
            });
        }

        return entries;
    }

    private static IEnumerable<(string Path, bool IsDirectory)> Enumerate(string root, bool recursive)
    {
        DirectoryInfo rootInfo = new(root);
        yield return (rootInfo.FullName, true);

        if (!recursive || rootInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            yield break;
        }

        Stack<DirectoryInfo> pending = new();
        pending.Push(rootInfo);

        while (pending.Count > 0)
        {
            DirectoryInfo current = pending.Pop();
            IReadOnlyList<string> childDirectories;

            try
            {
                childDirectories = Directory.EnumerateDirectories(current.FullName).ToList();
            }
            catch (UnauthorizedAccessException)
            {
                childDirectories = [];
            }
            catch (IOException)
            {
                childDirectories = [];
            }

            foreach (string childDirectoryPath in childDirectories)
            {
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(childDirectoryPath);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

                yield return (childDirectoryPath, true);
                pending.Push(new DirectoryInfo(childDirectoryPath));
            }

            IReadOnlyList<string> childFiles;
            try
            {
                childFiles = Directory.EnumerateFiles(current.FullName).ToList();
            }
            catch (UnauthorizedAccessException)
            {
                childFiles = [];
            }
            catch (IOException)
            {
                childFiles = [];
            }

            foreach (string childFilePath in childFiles)
            {
                try
                {
                    if (File.GetAttributes(childFilePath).HasFlag(FileAttributes.ReparsePoint))
                    {
                        continue;
                    }
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                yield return (childFilePath, false);
            }
        }
    }
}
