using FolderGate.Core.Localization;
using FolderGate.Core.Security;

namespace FolderGate.Core.Tests;

[TestClass]
public sealed class PasswordServiceTests
{
    [TestMethod]
    public void CreatePasswordRecord_StoresOnlyHashMaterial()
    {
        PasswordService service = new();

        var record = service.CreatePasswordRecord("correct horse battery staple");

        Assert.AreEqual("PBKDF2-SHA256", record.Algorithm);
        Assert.IsTrue(record.Iterations >= 100_000);
        Assert.IsFalse(string.IsNullOrWhiteSpace(record.SaltBase64));
        Assert.IsFalse(string.IsNullOrWhiteSpace(record.HashBase64));
        Assert.IsTrue(service.Verify("correct horse battery staple", record));
        Assert.IsFalse(service.Verify("wrong password", record));
    }

    [TestMethod]
    public void CreatePasswordRecord_RejectsThreeCharacterPassword()
    {
        PasswordService service = new();

        ArgumentException exception = Assert.ThrowsException<ArgumentException>(() => service.CreatePasswordRecord("abc"));

        StringAssert.Contains(exception.Message, AppText.PasswordTooShort);
    }

    [TestMethod]
    public void CreatePasswordRecord_AllowsFourCharacterPassword()
    {
        PasswordService service = new();

        var record = service.CreatePasswordRecord("1234");

        Assert.IsTrue(service.Verify("1234", record));
    }
}
