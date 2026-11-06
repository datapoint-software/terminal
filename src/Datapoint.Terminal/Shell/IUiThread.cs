namespace Datapoint.Terminal.Shell;

// An interface so view models can be tested without Avalonia.
public interface IUiThread
{
    void Post(Action action);
}
