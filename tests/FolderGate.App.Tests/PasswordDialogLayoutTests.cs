using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FolderGate.App.Tests;

[TestClass]
public sealed class PasswordDialogLayoutTests
{
    [TestMethod]
    public void NewPasswordDialog_UsesAutoHeightAndSafeMinimumSize()
    {
        RunOnStaThread(() =>
        {
            PasswordDialog dialog = PasswordDialog.CreateNewPasswordPrompt(
                "비밀번호 설정",
                "처음 등록하는 폴더입니다. 잠금 해제에 사용할 비밀번호를 설정하세요.");

            try
            {
                Assert.AreEqual(SizeToContent.Height, dialog.SizeToContent);
                Assert.IsTrue(double.IsNaN(dialog.Height), "Height must not be fixed.");
                Assert.IsTrue(dialog.MinHeight >= 300);
                Assert.IsTrue(dialog.MinWidth >= 440);
                Assert.AreEqual(ResizeMode.NoResize, dialog.ResizeMode);
            }
            finally
            {
                dialog.Close();
            }
        });
    }

    [TestMethod]
    public void NewPasswordDialog_DoesNotClipControlsAtCommonDpiScales()
    {
        RunOnStaThread(() =>
        {
            foreach (double scale in new[] { 1.0, 1.25, 1.5 })
            {
                PasswordDialog dialog = PasswordDialog.CreateNewPasswordPrompt(
                    "비밀번호 설정",
                    "처음 등록하는 폴더입니다. 잠금 해제에 사용할 비밀번호를 설정하세요. 한글 문구가 길어져도 컨트롤이 겹치지 않아야 합니다.");

                try
                {
                    dialog.LayoutTransform = new ScaleTransform(scale, scale);
                    FrameworkElement content = (FrameworkElement)dialog.Content;
                    content.LayoutTransform = new ScaleTransform(scale, scale);
                    content.Measure(new Size(dialog.Width, double.PositiveInfinity));
                    dialog.Measure(new Size(dialog.Width, double.PositiveInfinity));
                    dialog.Arrange(new Rect(0, 0, dialog.DesiredSize.Width, dialog.DesiredSize.Height));
                    dialog.UpdateLayout();

                    AssertVisibleControl(dialog, "PasswordInput", scale);
                    AssertVisibleControl(dialog, "ConfirmInput", scale);
                    AssertVisibleControl(dialog, "OkButton", scale);
                    AssertVisibleControl(dialog, "CancelButton", scale);
                    Assert.IsTrue(content.DesiredSize.Height > 0, $"Dialog content did not measure at scale {scale:P0}.");
                    Assert.AreEqual(SizeToContent.Height, dialog.SizeToContent, $"Dialog must grow vertically at scale {scale:P0}.");
                    Assert.IsTrue(double.IsNaN(dialog.Height), $"Dialog height must remain auto at scale {scale:P0}.");
                }
                finally
                {
                    dialog.Close();
                }
            }
        });
    }

    private static void AssertVisibleControl(FrameworkElement root, string name, double scale)
    {
        FrameworkElement element = (FrameworkElement)root.FindName(name);
        Assert.IsNotNull(element, $"{name} not found at scale {scale:P0}.");
        Assert.IsTrue(element.MinHeight >= 34, $"{name} MinHeight is too small at scale {scale:P0}.");
        Assert.AreEqual(Visibility.Visible, element.Visibility, $"{name} is not visible at scale {scale:P0}.");
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? captured = null;
        Thread thread = new(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (captured is not null)
        {
            throw captured;
        }
    }
}
