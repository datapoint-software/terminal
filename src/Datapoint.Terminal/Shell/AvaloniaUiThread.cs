using Avalonia.Threading;

namespace Datapoint.Terminal.Shell;

public sealed class AvaloniaUiThread : IUiThread
{
    public void Post(Action action) => Dispatcher.UIThread.Post(action);
}
