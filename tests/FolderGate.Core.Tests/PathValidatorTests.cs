using FolderGate.Core.Localization;
using FolderGate.Core.Storage;
using FolderGate.Core.Validation;

namespace FolderGate.Core.Tests;

[TestClass]
public sealed class PathValidatorTests
{
    [TestMethod]
    public void ValidateDirectory_RejectsProjectRoot()
    {
        AppPaths paths = AppPaths.Resolve();
        TargetPathValidator validator = new(paths);

        PathValidationResult result = validator.ValidateDirectory(paths.ProjectRoot);

        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(AppText.ProjectFolderBlocked, result.ErrorMessage);
    }

    [TestMethod]
    public void WindowsPathComparer_UsesCaseInsensitiveComparison()
    {
        string path = Path.Combine("C:\\", "Temp", "Folder");

        Assert.IsTrue(WindowsPathComparer.AreSamePath(path, path.ToUpperInvariant()));
    }
}
