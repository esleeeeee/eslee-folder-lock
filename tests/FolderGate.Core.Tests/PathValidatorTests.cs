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
        StringAssert.Contains(result.ErrorMessage, "프로젝트");
    }

    [TestMethod]
    public void WindowsPathComparer_UsesCaseInsensitiveComparison()
    {
        string path = Path.Combine("C:\\", "Temp", "Folder");

        Assert.IsTrue(WindowsPathComparer.AreSamePath(path, path.ToUpperInvariant()));
    }
}
