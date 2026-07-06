namespace FolderGate.Core.Models;

public sealed class PasswordRecord
{
    public string Algorithm { get; set; } = "PBKDF2-SHA256";

    public int Iterations { get; set; }

    public string SaltBase64 { get; set; } = string.Empty;

    public string HashBase64 { get; set; } = string.Empty;
}
