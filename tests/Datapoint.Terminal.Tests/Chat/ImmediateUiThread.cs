using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal.Tests.Chat;

internal sealed class ImmediateUiThread : IUiThread
{
    public void Post(Action action) => action();
}
