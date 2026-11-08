using CommunityToolkit.Mvvm.ComponentModel;

namespace Datapoint.Terminal.Resources;

// A view model whose properties read text from Strings: when the language changes, it tells its views every property
// may have changed, so they read the text again (product decision 150).
public abstract class LocalizedObject : ObservableObject, ILocalizable
{
    protected LocalizedObject() => Localizer.Instance.Register(this);

    public virtual void OnLanguageChanged() => OnPropertyChanged(string.Empty);
}
