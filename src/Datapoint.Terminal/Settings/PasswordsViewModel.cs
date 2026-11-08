using CommunityToolkit.Mvvm.ComponentModel;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Security.Credentials;

namespace Datapoint.Terminal.Settings;

// Settings › Passwords (product decision 113): the SSH logins whose password is saved, whether each is used without asking,
// and forgetting them. Passwords are saved where they're typed, in the login window, so this page never shows one.
public sealed partial class PasswordsViewModel : ObservableObject
{
    private readonly SettingsViewModel _settings;

    public PasswordsViewModel(SettingsViewModel settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        settings.Changed += (_, _) => Load();
        Load();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLogins))]
    public partial IReadOnlyList<SavedLoginViewModel> Logins { get; private set; } = [];

    public bool HasLogins => Logins.Count > 0;

    public string Hint => _settings.LoginPasswords.CanSave
        ? Strings.Format(Strings.PasswordsHint, _settings.LoginPasswords.StoreName)
        : Strings.PasswordsNoStore;

    [ObservableProperty]
    public partial string Status { get; private set; } = string.Empty;

    private void Forget(string login)
    {
        try
        {
            _settings.ForgetLogin(login);
            Status = Strings.Format(Strings.PasswordsForgotten, login);
        }
        catch (CredentialStoreException exception)
        {
            Status = Strings.Format(Strings.PasswordsNotForgotten, login, exception.Message);
        }
    }

    private void Load() =>
        Logins = [.. _settings.SavedLogins.Select((login, index) => new SavedLoginViewModel(login, index == 0, _settings.SetUsesWithoutAsking, Forget))];
}
