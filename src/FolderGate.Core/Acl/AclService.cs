using System.Security.AccessControl;
using System.Security.Principal;
using FolderGate.Core.Localization;
using FolderGate.Core.Models;
using FolderGate.Core.Storage;

namespace FolderGate.Core.Acl;

public sealed class AclService
{
    private static readonly FileSystemRights DeniedRights =
        FileSystemRights.ListDirectory |
        FileSystemRights.ReadData |
        FileSystemRights.WriteData |
        FileSystemRights.CreateFiles |
        FileSystemRights.AppendData |
        FileSystemRights.CreateDirectories |
        FileSystemRights.WriteAttributes |
        FileSystemRights.WriteExtendedAttributes |
        FileSystemRights.Delete |
        FileSystemRights.DeleteSubdirectoriesAndFiles;

    private const AccessControlSections BackupSections =
        AccessControlSections.Access |
        AccessControlSections.Owner |
        AccessControlSections.Group;

    private readonly AclBackupStore _backupStore;
    private readonly JsonOperationLogger _logger;
    private readonly IAclFaultInjector _faultInjector;

    public AclService(AppPaths paths)
        : this(new AclBackupStore(paths), new JsonOperationLogger(paths))
    {
    }

    public AclService(AclBackupStore backupStore, JsonOperationLogger logger)
        : this(backupStore, logger, NoAclFaultInjector.Instance)
    {
    }

    internal AclService(AclBackupStore backupStore, JsonOperationLogger logger, IAclFaultInjector faultInjector)
    {
        _backupStore = backupStore;
        _logger = logger;
        _faultInjector = faultInjector;
    }

    public Task<AclBackupFile> CreateBackupAsync(RegisteredFolder folder, LockMode mode, string operationId, CancellationToken cancellationToken, IProgress<AclOperationProgress>? progress = null)
    {
        return Task.Run(() =>
        {
            IReadOnlyList<AclBackupEntry> entries = FileSystemTargetEnumerator.CreateEmptyEntries(folder.Path, mode == LockMode.Hardened, cancellationToken, progress);
            int total = entries.Count;
            int processed = 0;
            int failed = 0;

            foreach (AclBackupEntry entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    entry.Sddl = CaptureSddl(entry.Path, entry.IsDirectory);
                    entry.CapturedUtc = DateTimeOffset.UtcNow;
                    processed++;
                    progress?.Report(new AclOperationProgress
                    {
                        Phase = "backup",
                        Processed = processed,
                        Total = total,
                        Failed = failed,
                        CurrentPath = entry.Path
                    });
                }
                catch
                {
                    failed++;
                    progress?.Report(new AclOperationProgress
                    {
                        Phase = "backup",
                        Processed = processed,
                        Total = total,
                        Failed = failed,
                        CurrentPath = entry.Path
                    });
                    throw;
                }
            }

            return new AclBackupFile
            {
                OperationId = operationId,
                TargetId = folder.Id,
                TargetPath = folder.Path,
                Mode = mode,
                Entries = entries.ToList()
            };
        }, cancellationToken);
    }

    public async Task<AclOperationResult> LockAsync(RegisteredFolder folder, LockMode mode, CancellationToken cancellationToken, IProgress<AclOperationProgress>? progress = null, string? operationId = null)
    {
        operationId ??= Guid.NewGuid().ToString("N");
        string backupPath = _backupStore.CreateBackupPath(folder.Id, operationId);
        List<AclBackupEntry> changed = [];
        SecurityIdentifier ownerSid = ResolveOwnerSid(folder);
        int failed = 0;

        try
        {
            _logger.Info(operationId, folder.Id, "Lock", folder.Path, mode == LockMode.Hardened
                ? AppText.HardenedModeNoPerItemExternalProcess
                : AppText.StartingAclBackup);
            AclBackupFile backup = await CreateBackupAsync(folder, mode, operationId, cancellationToken, progress).ConfigureAwait(false);
            _backupStore.Save(backupPath, backup);

            int total = backup.Entries.Count;
            int processed = 0;

            foreach (AclBackupEntry entry in backup.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    _faultInjector.BeforeApplyDeny(entry, processed + 1);
                    ApplyDenyRule(entry.Path, entry.IsDirectory, ownerSid);
                    changed.Add(entry);
                    processed++;
                    progress?.Report(new AclOperationProgress
                    {
                        Phase = "lock",
                        Processed = processed,
                        Total = total,
                        Failed = failed,
                        CurrentPath = entry.Path
                    });
                }
                catch
                {
                    failed++;
                    progress?.Report(new AclOperationProgress
                    {
                        Phase = "lock",
                        Processed = processed,
                        Total = total,
                        Failed = failed,
                        CurrentPath = entry.Path
                    });
                    throw;
                }
            }

            _logger.Info(operationId, folder.Id, "Lock", folder.Path, AppText.LockCompleted);
            return AclOperationResult.Ok(operationId, backupPath, changed.Count, AppText.LockCompleted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.Failure(operationId, folder.Id, "Lock", folder.Path, ex);
            bool restoreOk = TryRestoreChangedEntries(operationId, folder.Id, changed, progress);
            string message = restoreOk
                ? AppText.LockErrorRolledBack
                : AppText.LockErrorRecoveryRequired;

            return AclOperationResult.Failed(operationId, backupPath, changed.Count, message, recoveryRequired: !restoreOk, failedCount: Math.Max(1, failed));
        }
        catch (OperationCanceledException ex)
        {
            _logger.Failure(operationId, folder.Id, "LockCanceled", folder.Path, ex);
            bool restoreOk = TryRestoreChangedEntries(operationId, folder.Id, changed, progress);
            string message = restoreOk
                ? AppText.LockCanceledRolledBack
                : AppText.LockCanceledRecoveryRequired;

            return AclOperationResult.Failed(operationId, backupPath, changed.Count, message, recoveryRequired: !restoreOk, failedCount: failed);
        }
    }

    public Task<AclOperationResult> UnlockAsync(RegisteredFolder folder, string? backupPath, CancellationToken cancellationToken, IProgress<AclOperationProgress>? progress = null, string? operationId = null)
    {
        return Task.Run(() =>
        {
            operationId ??= Guid.NewGuid().ToString("N");
            SecurityIdentifier ownerSid = ResolveOwnerSid(folder);
            int failed = 0;

            try
            {
                AclBackupFile? backup = null;
                if (!string.IsNullOrWhiteSpace(backupPath) && File.Exists(backupPath))
                {
                    backup = _backupStore.Load(backupPath);
                }

                IReadOnlyList<AclBackupEntry> entries = backup?.Entries
                    ?? FileSystemTargetEnumerator.CreateEmptyEntries(folder.Path, folder.Mode == LockMode.Hardened);

                int total = entries.Count;
                int processed = 0;

                foreach (AclBackupEntry entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        RemoveDenyRulesAddedAfterBackup(entry.Path, entry.IsDirectory, entry.Sddl, ownerSid);
                        processed++;
                        progress?.Report(new AclOperationProgress
                        {
                            Phase = "unlock",
                            Processed = processed,
                            Total = total,
                            Failed = failed,
                            CurrentPath = entry.Path
                        });
                    }
                    catch
                    {
                        failed++;
                        progress?.Report(new AclOperationProgress
                        {
                            Phase = "unlock",
                            Processed = processed,
                            Total = total,
                            Failed = failed,
                            CurrentPath = entry.Path
                        });
                        throw;
                    }
                }

                _logger.Info(operationId, folder.Id, "Unlock", folder.Path, AppText.UnlockCompleted);
                return AclOperationResult.Ok(operationId, backupPath, processed, AppText.UnlockCompleted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.Failure(operationId, folder.Id, "Unlock", folder.Path, ex);
                return AclOperationResult.Failed(operationId, backupPath, 0, AppText.UnlockErrorNoAclReset, recoveryRequired: true, failedCount: Math.Max(1, failed));
            }
            catch (OperationCanceledException ex)
            {
                _logger.Failure(operationId, folder.Id, "UnlockCanceled", folder.Path, ex);
                return AclOperationResult.Failed(operationId, backupPath, 0, AppText.UnlockCanceledNoAclReset, recoveryRequired: false, failedCount: failed);
            }
        }, cancellationToken);
    }

    public Task<AclOperationResult> RestoreBackupAsync(string targetId, string backupPath, CancellationToken cancellationToken, IProgress<AclOperationProgress>? progress = null, string? operationId = null)
    {
        return Task.Run(() =>
        {
            operationId ??= Guid.NewGuid().ToString("N");
            AclBackupFile backup = _backupStore.Load(backupPath);
            int total = backup.Entries.Count;
            int processed = 0;
            int failed = 0;

            try
            {
                foreach (AclBackupEntry entry in backup.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        RestoreEntry(entry);
                        processed++;
                        progress?.Report(new AclOperationProgress
                        {
                            Phase = "restore",
                            Processed = processed,
                            Total = total,
                            Failed = failed,
                            CurrentPath = entry.Path
                        });
                    }
                    catch
                    {
                        failed++;
                        progress?.Report(new AclOperationProgress
                        {
                            Phase = "restore",
                            Processed = processed,
                            Total = total,
                            Failed = failed,
                            CurrentPath = entry.Path
                        });
                        throw;
                    }
                }

                _logger.Info(operationId, targetId, "Restore", backup.TargetPath, AppText.RestoreCompleted);
                return AclOperationResult.Ok(operationId, backupPath, processed, AppText.RestoreCompleted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.Failure(operationId, targetId, "Restore", backup.TargetPath, ex);
                return AclOperationResult.Failed(operationId, backupPath, processed, AppText.RestoreError, recoveryRequired: true, failedCount: Math.Max(1, failed));
            }
            catch (OperationCanceledException ex)
            {
                _logger.Failure(operationId, targetId, "RestoreCanceled", backup.TargetPath, ex);
                return AclOperationResult.Failed(operationId, backupPath, processed, AppText.RestoreCanceled, recoveryRequired: true, failedCount: failed);
            }
        }, cancellationToken);
    }

    private static string CaptureSddl(string path, bool isDirectory)
    {
        FileSystemSecurity security = isDirectory
            ? new DirectoryInfo(path).GetAccessControl(BackupSections)
            : new FileInfo(path).GetAccessControl(BackupSections);

        return security.GetSecurityDescriptorSddlForm(BackupSections);
    }

    private static void ApplyDenyRule(string path, bool isDirectory, SecurityIdentifier ownerSid)
    {
        FileSystemAccessRule rule = CreateFolderGateRule(ownerSid);

        if (isDirectory)
        {
            DirectoryInfo directory = new(path);
            DirectorySecurity security = directory.GetAccessControl(AccessControlSections.Access);
            security.AddAccessRule(rule);
            directory.SetAccessControl(security);
        }
        else
        {
            FileInfo file = new(path);
            FileSecurity security = file.GetAccessControl(AccessControlSections.Access);
            security.AddAccessRule(rule);
            file.SetAccessControl(security);
        }
    }

    private static void RemoveDenyRulesAddedAfterBackup(string path, bool isDirectory, string? originalSddl, SecurityIdentifier ownerSid)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return;
        }

        if (isDirectory)
        {
            DirectoryInfo directory = new(path);
            DirectorySecurity current = directory.GetAccessControl(AccessControlSections.Access);
            int originalCount = CountMatchingRules(BuildOriginalDirectorySecurity(originalSddl), ownerSid);
            bool changed = RemoveExcessMatchingRules(current, ownerSid, originalCount);
            if (changed)
            {
                directory.SetAccessControl(current);
            }
        }
        else
        {
            FileInfo file = new(path);
            FileSecurity current = file.GetAccessControl(AccessControlSections.Access);
            int originalCount = CountMatchingRules(BuildOriginalFileSecurity(originalSddl), ownerSid);
            bool changed = RemoveExcessMatchingRules(current, ownerSid, originalCount);
            if (changed)
            {
                file.SetAccessControl(current);
            }
        }
    }

    private static bool RemoveExcessMatchingRules(FileSystemSecurity security, SecurityIdentifier ownerSid, int originalCount)
    {
        List<FileSystemAccessRule> matches = security
            .GetAccessRules(includeExplicit: true, includeInherited: false, targetType: typeof(SecurityIdentifier))
            .OfType<FileSystemAccessRule>()
            .Where(rule => IsFolderGateRule(rule, ownerSid))
            .ToList();

        int removeCount = Math.Max(0, matches.Count - originalCount);
        if (removeCount == 0)
        {
            return false;
        }

        foreach (FileSystemAccessRule rule in matches.Take(removeCount))
        {
            security.RemoveAccessRuleSpecific(rule);
        }

        return true;
    }

    private static int CountMatchingRules(FileSystemSecurity? security, SecurityIdentifier ownerSid)
    {
        if (security is null)
        {
            return 0;
        }

        return security
            .GetAccessRules(includeExplicit: true, includeInherited: false, targetType: typeof(SecurityIdentifier))
            .OfType<FileSystemAccessRule>()
            .Count(rule => IsFolderGateRule(rule, ownerSid));
    }

    private static DirectorySecurity? BuildOriginalDirectorySecurity(string? sddl)
    {
        if (string.IsNullOrWhiteSpace(sddl))
        {
            return null;
        }

        DirectorySecurity security = new();
        security.SetSecurityDescriptorSddlForm(sddl, BackupSections);
        return security;
    }

    private static FileSecurity? BuildOriginalFileSecurity(string? sddl)
    {
        if (string.IsNullOrWhiteSpace(sddl))
        {
            return null;
        }

        FileSecurity security = new();
        security.SetSecurityDescriptorSddlForm(sddl, BackupSections);
        return security;
    }

    private static void RestoreEntry(AclBackupEntry entry)
    {
        if (!File.Exists(entry.Path) && !Directory.Exists(entry.Path))
        {
            return;
        }

        if (entry.IsDirectory)
        {
            DirectoryInfo directory = new(entry.Path);
            DirectorySecurity security = new();
            security.SetSecurityDescriptorSddlForm(entry.Sddl, BackupSections);
            directory.SetAccessControl(security);
        }
        else
        {
            FileInfo file = new(entry.Path);
            FileSecurity security = new();
            security.SetSecurityDescriptorSddlForm(entry.Sddl, BackupSections);
            file.SetAccessControl(security);
        }
    }

    private static FileSystemAccessRule CreateFolderGateRule(SecurityIdentifier ownerSid)
    {
        return new FileSystemAccessRule(
            ownerSid,
            DeniedRights,
            InheritanceFlags.None,
            PropagationFlags.None,
            AccessControlType.Deny);
    }

    private static bool IsFolderGateRule(FileSystemAccessRule rule, SecurityIdentifier ownerSid)
    {
        return rule.AccessControlType == AccessControlType.Deny &&
               rule.IdentityReference == ownerSid &&
               rule.InheritanceFlags == InheritanceFlags.None &&
               rule.PropagationFlags == PropagationFlags.None &&
               (rule.FileSystemRights & DeniedRights) == DeniedRights;
    }

    private static SecurityIdentifier ResolveOwnerSid(RegisteredFolder folder)
    {
        if (!string.IsNullOrWhiteSpace(folder.OwnerSid))
        {
            return new SecurityIdentifier(folder.OwnerSid);
        }

        return WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException(AppText.CurrentUserSidUnavailable);
    }

    private bool TryRestoreChangedEntries(string operationId, string targetId, IReadOnlyList<AclBackupEntry> changedEntries, IProgress<AclOperationProgress>? progress)
    {
        bool ok = true;
        int processed = 0;
        int failed = 0;
        foreach (AclBackupEntry entry in changedEntries.Reverse())
        {
            try
            {
                RestoreEntry(entry);
            }
            catch (Exception ex)
            {
                ok = false;
                failed++;
                _logger.Failure(operationId, targetId, "Rollback", entry.Path, ex);
            }

            processed++;
            progress?.Report(new AclOperationProgress
            {
                Phase = "rollback",
                Processed = processed,
                Total = changedEntries.Count,
                Failed = failed,
                CurrentPath = entry.Path
            });
        }

        return ok;
    }
}
