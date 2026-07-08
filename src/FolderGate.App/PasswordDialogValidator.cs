using FolderGate.Core.Security;
using FolderGate.Core.Localization;

namespace FolderGate.App;

public static class PasswordDialogValidator
{
    public static string? Validate(string password, string? confirmation, bool requiresConfirmation)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < PasswordService.MinimumPasswordLength)
        {
            return AppText.PasswordTooShort;
        }

        if (requiresConfirmation && !string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            return AppText.PasswordConfirmMismatch;
        }

        return null;
    }
}
