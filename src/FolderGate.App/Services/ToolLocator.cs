using System.IO;
using FolderGate.Core.Storage;

namespace FolderGate.App.Services;

public sealed class ToolLocator
{
    private static readonly IReadOnlyDictionary<string, string[]> DisplayExecutableNames = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["FolderGate.App"] = ["이은성폴더잠금기.exe"],
        ["FolderGate.ElevatedHelper"] = ["이은성폴더잠금기_권한도우미.exe"],
        ["FolderGate.RecoveryTool"] = ["이은성폴더잠금기_복구도구.exe"]
    };

    private readonly AppPaths _paths;

    public ToolLocator(AppPaths paths)
    {
        _paths = paths;
    }

    public string FindExecutable(string projectName)
    {
        string internalExeName = projectName + ".exe";
        string[] exeNames = DisplayExecutableNames.TryGetValue(projectName, out string[]? displayNames)
            ? [.. displayNames, internalExeName]
            : [internalExeName];

        string[] searchDirectories =
        [
            AppContext.BaseDirectory,
            _paths.ReleaseDirectory,
            Path.Combine(_paths.ProjectRoot, "src", projectName, "bin", "Debug", "net8.0-windows"),
            Path.Combine(_paths.ProjectRoot, "src", projectName, "bin", "Release", "net8.0-windows")
        ];

        IEnumerable<string> candidates = searchDirectories.SelectMany(directory => exeNames.Select(exeName => Path.Combine(directory, exeName)));

        string? match = candidates.FirstOrDefault(File.Exists);
        if (match is null)
        {
            throw new FileNotFoundException($"{projectName} 실행 파일을 찾을 수 없습니다. 먼저 솔루션을 빌드하거나 release 폴더를 다시 생성하세요.", internalExeName);
        }

        return match;
    }
}
