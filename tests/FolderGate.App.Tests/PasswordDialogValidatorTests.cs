using FolderGate.Core.Localization;

namespace FolderGate.App.Tests;

[TestClass]
public sealed class PasswordDialogValidatorTests
{
    [TestMethod]
    public void Validate_RejectsThreeCharacters()
    {
        string? error = PasswordDialogValidator.Validate("abc", "abc", requiresConfirmation: true);

        Assert.AreEqual(AppText.PasswordTooShort, error);
    }

    [TestMethod]
    public void Validate_AllowsFourCharactersWhenConfirmationMatches()
    {
        string? error = PasswordDialogValidator.Validate("1234", "1234", requiresConfirmation: true);

        Assert.IsNull(error);
    }

    [TestMethod]
    public void Validate_RejectsConfirmationMismatch()
    {
        string? error = PasswordDialogValidator.Validate("1234", "4321", requiresConfirmation: true);

        Assert.AreEqual(AppText.PasswordConfirmMismatch, error);
    }
}
