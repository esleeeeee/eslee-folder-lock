using FolderGate.App.Services;

namespace FolderGate.App.Tests;

[TestClass]
public sealed class StartupRelockServiceTests
{
    [TestMethod]
    public void BuildCommand_QuotesExecutableAndRoot()
    {
        string command = StartupRelockService.BuildCommand(
            @"C:\Program Files\eslee folder locker\eslee폴더잠금기.exe",
            @"C:\Users\dldms\Project Root");

        Assert.AreEqual(
            "\"C:\\Program Files\\eslee folder locker\\eslee폴더잠금기.exe\" --resume-temporary-unlocks --root \"C:\\Users\\dldms\\Project Root\"",
            command);
    }
}
