using System.ComponentModel;
using System.Diagnostics;
using FolderGate.Core.Models;
using FolderGate.Core.Storage;

namespace FolderGate.App.Services;

public sealed class ElevatedToolRunner
{
    private readonly AppPaths _paths;
    private readonly ToolLocator _toolLocator;

    public ElevatedToolRunner(AppPaths paths, ToolLocator toolLocator)
    {
        _paths = paths;
        _toolLocator = toolLocator;
    }

    public Task<int> RunHelperAsync(string command, RegisteredFolder folder, string operationId, LockMode? mode = null)
    {
        string helperPath = _toolLocator.FindExecutable("FolderGate.ElevatedHelper");
        ProcessStartInfo startInfo = new()
        {
            FileName = helperPath,
            WorkingDirectory = _paths.ProjectRoot,
            UseShellExecute = true,
            Verb = "runas"
        };

        startInfo.ArgumentList.Add(command);
        startInfo.ArgumentList.Add("--root");
        startInfo.ArgumentList.Add(_paths.ProjectRoot);
        startInfo.ArgumentList.Add("--target-id");
        startInfo.ArgumentList.Add(folder.Id);
        startInfo.ArgumentList.Add("--operation-id");
        startInfo.ArgumentList.Add(operationId);

        if (mode is not null)
        {
            startInfo.ArgumentList.Add("--mode");
            startInfo.ArgumentList.Add(mode.Value.ToString());
        }

        return RunProcessAsync(startInfo);
    }

    public Task OpenRecoveryToolAsync()
    {
        string recoveryPath = _toolLocator.FindExecutable("FolderGate.RecoveryTool");
        ProcessStartInfo startInfo = new()
        {
            FileName = recoveryPath,
            WorkingDirectory = _paths.ProjectRoot,
            UseShellExecute = true,
            Verb = "runas"
        };
        startInfo.ArgumentList.Add("--root");
        startInfo.ArgumentList.Add(_paths.ProjectRoot);
        return RunProcessAsync(startInfo);
    }

    public static void OpenExplorer(string path)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = "explorer.exe",
            UseShellExecute = true
        };
        startInfo.ArgumentList.Add(path);
        Process.Start(startInfo);
    }

    private static async Task<int> RunProcessAsync(ProcessStartInfo startInfo)
    {
        try
        {
            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("프로세스를 시작하지 못했습니다.");
            await process.WaitForExitAsync().ConfigureAwait(true);
            return process.ExitCode;
        }
        catch (Win32Exception ex) when ((uint)ex.NativeErrorCode == 1223)
        {
            throw new InvalidOperationException("UAC 승격이 취소되었습니다.", ex);
        }
    }
}
