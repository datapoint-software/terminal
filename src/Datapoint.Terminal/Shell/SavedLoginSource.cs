using Datapoint.Terminal.Settings;

namespace Datapoint.Terminal.Shell;

// Where a login's saved password is looked up: Login is user@host:port as LoginPasswords.Login gives it.
public sealed record SavedLoginSource(SettingsViewModel Settings, string Login, string User, string Host);
