using Datapoint.Terminal.Shell.Notifications;

namespace Datapoint.Terminal.Tests.Shell;

public sealed class DesktopNotificationsTests
{
    [Fact]
    public void Passes_the_text_as_arguments_never_as_script()
    {
        var start = DesktopNotifications.Command("web \"project\"", "it's done\" & do shell script \"rm -rf ~\"");

        if (start is null)
        {
            return;
        }

        Assert.False(start.UseShellExecute);
        Assert.Equal("it's done\" & do shell script \"rm -rf ~\"", start.ArgumentList[^1]);
        Assert.Equal("web \"project\"", start.ArgumentList[^2]);
        Assert.DoesNotContain(start.ArgumentList.Take(start.ArgumentList.Count - 2), argument => argument.Contains("rm -rf", StringComparison.Ordinal));
    }
}
