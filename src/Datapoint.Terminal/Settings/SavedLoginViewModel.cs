using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Settings;

// One saved login on the Passwords page: whether it's used without asking, and a button to forget it.
public sealed partial class SavedLoginViewModel : LocalizedObject
{
    private readonly Action<string, bool>? _setUsesWithoutAsking;
    private readonly Action<string> _forget;

    public SavedLoginViewModel(SavedLogin login, bool isFirst, Action<string, bool> setUsesWithoutAsking, Action<string> forget)
    {
        ArgumentNullException.ThrowIfNull(login);
        Login = login.Login;
        IsFirst = isFirst;
        UsesWithoutAsking = login.UsesWithoutAsking;
        _setUsesWithoutAsking = setUsesWithoutAsking;
        _forget = forget;
    }

    public string Login { get; }

    public bool IsFirst { get; }

    public string UsesWithoutAskingName => Strings.Format(Strings.PasswordsAlwaysName, Login);

    public string ForgetName => Strings.Format(Strings.PasswordsForgetName, Login);

    [ObservableProperty]
    public partial bool UsesWithoutAsking { get; set; }

    partial void OnUsesWithoutAskingChanged(bool value) => _setUsesWithoutAsking?.Invoke(Login, value);

    [RelayCommand]
    private void Forget() => _forget(Login);
}
