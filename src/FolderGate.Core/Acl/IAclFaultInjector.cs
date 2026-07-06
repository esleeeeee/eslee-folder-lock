namespace FolderGate.Core.Acl;

internal interface IAclFaultInjector
{
    void BeforeApplyDeny(AclBackupEntry entry, int attemptedIndex);
}

internal sealed class NoAclFaultInjector : IAclFaultInjector
{
    public static NoAclFaultInjector Instance { get; } = new();

    private NoAclFaultInjector()
    {
    }

    public void BeforeApplyDeny(AclBackupEntry entry, int attemptedIndex)
    {
    }
}
