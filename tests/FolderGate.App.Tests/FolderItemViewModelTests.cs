using FolderGate.App.ViewModels;
using FolderGate.Core.Localization;
using FolderGate.Core.Models;

namespace FolderGate.App.Tests;

[TestClass]
public sealed class FolderItemViewModelTests
{
    [TestMethod]
    public void StateText_ReturnsTemporaryUnlockedText()
    {
        FolderItemViewModel viewModel = new(new RegisteredFolder
        {
            State = FolderLockState.TemporarilyUnlocked
        });

        Assert.AreEqual(AppText.StateName(FolderLockState.TemporarilyUnlocked), viewModel.StateText);
    }
}
