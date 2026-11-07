namespace Datapoint.Terminal.Shell;

public sealed partial class MainWindow
{
    // The agent's web permission follows the settings, which the user can change from any window.
    private void ApplyWebSettings()
    {
        _chat.IsModelLocal = _provider?.IsLocal == true;
        if (_agent is not { } agent)
        {
            return;
        }

        agent.IsWebAlwaysAllowed = _settings.AlwaysAllowWeb;
    }
}
