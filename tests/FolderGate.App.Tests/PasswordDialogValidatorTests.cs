namespace FolderGate.App.Tests;

[TestClass]
public sealed class PasswordDialogValidatorTests
{
    [TestMethod]
    public void Validate_RejectsThreeCharacters()
    {
        string? error = PasswordDialogValidator.Validate("abc", "abc", requiresConfirmation: true);

        Assert.AreEqual("비밀번호는 최소 4자 이상이어야 합니다.", error);
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

        Assert.AreEqual("비밀번호 확인이 일치하지 않습니다.", error);
    }
}
