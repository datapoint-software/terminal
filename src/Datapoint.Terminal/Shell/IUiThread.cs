namespace Datapoint.Terminal.Shell;

/// <summary>Runs work on the user interface thread, so view models can be tested without Avalonia.</summary>
public interface IUiThread
{
    void Post(Action action);
}
