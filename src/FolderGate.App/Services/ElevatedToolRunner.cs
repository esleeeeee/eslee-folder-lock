using System.ComponentModel;
using System.Diagnostics;
using FolderGate.Core.Localization;
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

    public Task<int> RunHelperAsync(string command, RegisteredFolder folder, string operationId, LockMode? mode = null, TimeSpan? duration = null)
    {
        Process process = StartHelper(command, folder, operationId, mode, duration);
        return WaitForExitAsync(process);
    }

    public Process StartHelper(string command, RegisteredFolder folder, string operationId, LockMode? mode = null, TimeSpan? duration = null)
    {
        ProcessStartInfo startInfo = CreateHelperStartInfo(command, folder, operationId, mode, duration);
        try
        {
            return Process.Start(startInfo)
                ?? throw new InvalidOperationException(AppText.ProcessNotStarted);
        }
        catch (Win32Exception ex) when ((uint)ex.NativeErrorCode == 1223)
        {
            throw new InvalidOperationException(AppText.UacCanceled, ex);
        }
    }

    private ProcessStartInfo CreateHelperStartInfo(string command, RegisteredFolder folder, string operationId, LockMode? mode, TimeSpan? duration)
    {
        string helperPath = _toolLocator.FindExecutable("FolderGate.ElevatedHelper");
        ProcessStartInfo startInfo = new()
        {
            FileName = helperPath,
            WorkingDirectory = _paths.ProjectRoot,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
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

        if (duration is not null)
        {
            startInfo.ArgumentList.Add("--duration-seconds");
            startInfo.ArgumentList.Add(Math.Ceiling(duration.Value.TotalSeconds).ToString("0"));
        }

        return startInfo;
    }

    public Task OpenRecoveryToolAsync()
    {
        string recoveryPath = _toolLocator.FindExecutable("FolderGate.RecoveryTool");
        ProcessStartInfo startInfo = new()
        {
            FileName = recoveryPath,
            WorkingDirectory = _paths.ProjectRoot,
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
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
                ?? throw new InvalidOperationException(AppText.ProcessNotStarted);
            return await WaitForExitAsync(process).ConfigureAwait(true);
        }
        catch (Win32Exception ex) when ((uint)ex.NativeErrorCode == 1223)
        {
            throw new InvalidOperationException(AppText.UacCanceled, ex);
        }
    }

    private static async Task<int> WaitForExitAsync(Process process)
    {
        using (process)
        {
            await process.WaitForExitAsync().ConfigureAwait(true);
            return process.ExitCode;
        }
    }
}
