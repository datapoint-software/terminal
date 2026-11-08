namespace Datapoint.Terminal.Resources;

// Something on screen that builds text in code, and rebuilds it when the language changes.
public interface ILocalizable
{
    void OnLanguageChanged();
}
