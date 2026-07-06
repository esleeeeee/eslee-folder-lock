using FolderGate.Core.Security;

namespace FolderGate.App;

public static class PasswordDialogValidator
{
    public static string? Validate(string password, string? confirmation, bool requiresConfirmation)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < PasswordService.MinimumPasswordLength)
        {
            return $"비밀번호는 최소 {PasswordService.MinimumPasswordLength}자 이상이어야 합니다.";
        }

        if (requiresConfirmation && !string.Equals(password, confirmation, StringComparison.Ordinal))
        {
            return "비밀번호 확인이 일치하지 않습니다.";
        }

        return null;
    }
}
