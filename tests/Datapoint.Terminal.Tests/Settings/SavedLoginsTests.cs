using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Datapoint.Terminal.Agent.Safety;
using Datapoint.Terminal.Sessions.Ssh.Authentication;
using Datapoint.Terminal.Settings;
using Datapoint.Terminal.Shell;
using Datapoint.Terminal.Tests.Chat;
using Datapoint.Terminal.Tests.Shell;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Tests.Settings;

public sealed class SavedLoginsTests : IDisposable
{
    private const string Login = "joao@db1.example.org:22";

    private readonly string _folder = Directory.CreateTempSubdirectory("datapoint-saved-logins-").FullName;
    private readonly MemoryCredentialStore _store = new();

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string SettingsFile => Path.Combine(_folder, "settings.json");

    private SettingsViewModel Create()
    {
        var store = new SettingsStore(SettingsFile);
        return new SettingsViewModel(store, store.Load(), new ProviderKeys(_store, _ => null), (_, _) => new ListChatClient(new ChatResponse(new ChatMessage(ChatRole.Assistant, "OK"))));
    }

    [Fact]
    public void Names_a_login_by_user_address_and_port() =>
        Assert.Equal(Login, LoginPasswords.Login("joao", "DB1.example.org", 22));

    [Fact]
    public void Keeps_the_password_in_the_credential_store_and_only_the_login_in_settings()
    {
        var settings = Create();

        settings.SaveLoginPassword(Login, "correct horse battery staple");
        settings.SetUsesWithoutAsking(Login, usesWithoutAsking: true);
        settings.SaveLoginPassword(Login, "a newer password");

        Assert.Equal("a newer password", settings.LoginPasswords.Read(Login));
        var saved = Assert.Single(new SettingsStore(SettingsFile).Load().SavedLogins);
        Assert.Equal((Login, true), (saved.Login, saved.UsesWithoutAsking));
        Assert.DoesNotContain("password", File.ReadAllText(SettingsFile).Replace("savedLogins", string.Empty, StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase);

        settings.ForgetLogin(Login);
        Assert.Null(settings.LoginPasswords.Read(Login));
        Assert.Empty(settings.SavedLogins);
    }

    [AvaloniaFact]
    public async Task Gives_a_password_allowed_always_without_asking_and_masks_it()
    {
        var settings = Create();
        settings.SaveLoginPassword(Login, "correct horse battery staple");
        settings.SetUsesWithoutAsking(Login, usesWithoutAsking: true);
        var redactor = new SecretRedactor();
        var prompts = new SshLoginPrompts(new Window(), "db1", redactor, _ => { }, new SavedLoginSource(settings, Login, "joao", "db1.example.org"));

        var password = await prompts.ReadPasswordAsync("joao", "db1.example.org", CancellationToken.None);

        Assert.Equal("correct horse battery staple", password);
        Assert.DoesNotContain("correct horse", redactor.Redact("it was correct horse battery staple"), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Answers_a_servers_lone_password_question_with_the_saved_password()
    {
        var settings = Create();
        settings.SaveLoginPassword(Login, "correct horse battery staple");
        settings.SetUsesWithoutAsking(Login, usesWithoutAsking: true);
        var prompts = new SshLoginPrompts(new Window(), "db1", new SecretRedactor(), _ => { }, new SavedLoginSource(settings, Login, "joao", "db1.example.org"));

        var answers = await prompts.AnswerAsync(new KeyboardInteractiveRequest(string.Empty, string.Empty, [new KeyboardInteractivePrompt("Password: ", Echo: false)]), CancellationToken.None);

        Assert.Equal(["correct horse battery staple"], answers!, StringComparer.Ordinal);
    }

    [AvaloniaFact]
    public void Lists_saved_logins_and_forgets_them()
    {
        var settings = Create();
        settings.SaveLoginPassword(Login, "correct horse battery staple");
        settings.SaveLoginPassword("deploy@swarm-b.example.org:2222", "another");
        var window = new SettingsWindow { DataContext = settings, History = new FakeHistory() };
        window.Show();
        window.ShowPage(SettingsWindow.PasswordsPageIndex);
        var model = (PasswordsViewModel)window.FindControl<PasswordsPage>("PasswordsPage")!.DataContext!;

        model.Logins[1].UsesWithoutAsking = true;
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Save("rendered/settings-passwords.png", new PngBitmapEncoderOptions());
        model.Logins[0].ForgetCommand.Execute(null);

        var saved = Assert.Single(new SettingsStore(SettingsFile).Load().SavedLogins);
        Assert.Equal(("deploy@swarm-b.example.org:2222", true), (saved.Login, saved.UsesWithoutAsking));
        Assert.Null(settings.LoginPasswords.Read(Login));
        Assert.Single(model.Logins);
    }
}
