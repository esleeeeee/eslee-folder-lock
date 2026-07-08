using FolderGate.App.Services;

namespace FolderGate.App.Tests;

[TestClass]
public sealed class ExplorerContextMenuServiceTests
{
    [TestMethod]
    public void BuildCommand_QuotesExecutableRootAndExplorerPathPlaceholder()
    {
        string command = ExplorerContextMenuService.BuildCommand(
            @"C:\Program Files\eslee folder locker\eslee폴더잠금기.exe",
            @"C:\Users\dldms\Project Root");

        Assert.AreEqual(
            "\"C:\\Program Files\\eslee folder locker\\eslee폴더잠금기.exe\" --unlock-path \"%1\" --root \"C:\\Users\\dldms\\Project Root\"",
            command);
    }
}
