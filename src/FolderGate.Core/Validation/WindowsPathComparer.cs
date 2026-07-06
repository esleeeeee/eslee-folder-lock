namespace FolderGate.Core.Validation;

public static class WindowsPathComparer
{
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("경로가 비어 있습니다.", nameof(path));
        }

        string trimmed = path.Trim().Trim('"');
        string full = Path.GetFullPath(trimmed);
        string root = Path.GetPathRoot(full) ?? string.Empty;

        if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
        {
            return EnsureTrailingSeparator(full);
        }

        return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static bool AreSamePath(string left, string right)
    {
        return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSameOrChild(string candidate, string parent)
    {
        string normalizedCandidate = Normalize(candidate);
        string normalizedParent = Normalize(parent);

        if (string.Equals(normalizedCandidate, normalizedParent, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string parentWithSeparator = EnsureTrailingSeparator(normalizedParent);
        return normalizedCandidate.StartsWith(parentWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }
}
