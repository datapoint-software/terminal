using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Security.Credentials;
using Datapoint.Terminal.Settings;
using Datapoint.Terminal.Shell;
using Datapoint.Terminal.Themes;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Setup;

// Product decision 146: first-run setup, in four steps (welcome, AI provider, appearance, ready). Every step can be set aside,
// and everything in it can be changed later in Settings. Appearance applies as it's chosen, so the window itself is the preview.
public sealed partial class SetupViewModel : ObservableObject
{
    public const int StepCount = 4;

    private const int WelcomeStep = 0;
    private const int ProviderStep = 1;
    private const int AppearanceStep = 2;
    private const int ReadyStep = 3;

    private readonly SettingsViewModel _settings;
    private readonly Func<string, string?> _environment;
    private bool _isLoading = true;
    private bool _isClosed;

    public SetupViewModel(SettingsViewModel settings, Func<string, string?>? environment = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        _environment = environment ?? Environment.GetEnvironmentVariable;
        Choices =
        [
            new(ProviderKind.Anthropic, Strings.ProviderKindAnthropic, Strings.SetupAnthropicDetail),
            new(ProviderKind.OpenAI, Strings.ProviderKindOpenAI, Strings.SetupOpenAIDetail),
            new(ProviderKind.OpenAICompatible, Strings.SetupOpenWeightTitle, Strings.SetupOpenWeightDetail),
        ];
        Choice = Choices[0];
        SelectedWindowTheme = (int)settings.WindowTheme;
        SelectedTerminalTheme = Array.IndexOf(s_terminalThemes, settings.TerminalTheme) is var index and >= 0 ? index : 0;
        _isLoading = false;
    }

    // Raised when setup is done or set aside; the window closes.
    public event EventHandler? Done;

    public IReadOnlyList<ProviderChoice> Choices { get; }

    // In the order of WindowTheme's values.
    public IReadOnlyList<string> WindowThemes { get; } = [Strings.SetupThemeSystem, Strings.WindowThemeLight, Strings.WindowThemeDark, Strings.WindowThemeTerminal];

    public IReadOnlyList<string> TerminalThemes { get; } = [Strings.SetupThemeSystem, Strings.WindowThemeDark, Strings.WindowThemeLight];

    private static readonly TerminalTheme[] s_terminalThemes = [TerminalTheme.System, TerminalTheme.Dark, TerminalTheme.Light];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepText), nameof(IsWelcome), nameof(IsProvider), nameof(IsAppearance), nameof(IsReady), nameof(CanGoBack), nameof(CanSetUpLater), nameof(NextText))]
    [NotifyPropertyChangedFor(nameof(AgentSummary), nameof(WindowSummary), nameof(TerminalSummary))]
    public partial int Step { get; private set; }

    public string StepText => Strings.Format(Strings.SetupStep, Step + 1, StepCount);

    public bool IsWelcome => Step == WelcomeStep;

    public bool IsProvider => Step == ProviderStep;

    public bool IsAppearance => Step == AppearanceStep;

    public bool IsReady => Step == ReadyStep;

    public bool CanGoBack => Step > WelcomeStep;

    public bool CanSetUpLater => Step < ReadyStep;

    public string NextText => Step switch
    {
        WelcomeStep => Strings.SetupGetStarted,
        ReadyStep => Strings.SetupOpen,
        _ => Strings.SetupContinue,
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCompatible), nameof(NeedsKey), nameof(KeyNote), nameof(KeyPlaceholder), nameof(Privacy))]
    public partial ProviderChoice Choice { get; set; }

    public bool IsCompatible => Choice.Kind == ProviderKind.OpenAICompatible;

    public bool NeedsKey => ProviderCatalog.NeedsKey(Choice.Kind);

    public string KeyPlaceholder => Choice.Kind == ProviderKind.Anthropic ? "sk-ant-…" : "sk-…";

    [ObservableProperty]
    public partial string Key { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Privacy))]
    public partial string Endpoint { get; set; } = "http://localhost:11434/v1";

    [ObservableProperty]
    public partial string Model { get; set; } = string.Empty;

    // The user's own word that the server is theirs; a local address is likely, but not certain, to be.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Privacy))]
    public partial bool IsLocal { get; set; } = true;

    [ObservableProperty]
    public partial string Problem { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string TestResult { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool TestFailed { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestCommand))]
    public partial bool IsTesting { get; private set; }

    // Where the key goes, or where it comes from: the key itself is never shown or written anywhere but the credential store.
    public string KeyNote
    {
        get
        {
            var keys = _settings.Keys;
            var variable = VariableName(Choice.Kind);
            var fromEnvironment = variable is not null && !string.IsNullOrEmpty(_environment(variable));
            return (keys.CanSave, fromEnvironment) switch
            {
                (true, true) => Strings.Format(Strings.SetupKeyFromEnvironment, variable!, keys.StoreName!),
                (true, false) => Strings.Format(Strings.SetupKeySaved, keys.StoreName!),
                (false, _) => Strings.Format(Strings.ProviderKeyNoStore, variable ?? "ANTHROPIC_API_KEY"),
            };
        }
    }

    public string Privacy => ProviderPrivacy.Describe(Account()) is { Length: > 0 } sends ? sends + " " + Strings.SetupMasked : string.Empty;

    // Whether the provider step was completed, rather than set aside.
    public bool HasProvider { get; private set; }

    public string AgentSummary => HasProvider ? Strings.Format(Strings.SetupReadyAgent, ProviderPrivacy.Destination(Account())) : Strings.SetupReadyNoAgent;

    public string WindowSummary => Strings.Format(Strings.SetupReadyWindow, (WindowTheme)SelectedWindowTheme switch
    {
        WindowTheme.Light => Strings.SetupReadyLight,
        WindowTheme.Dark => Strings.SetupReadyDark,
        WindowTheme.Terminal => Strings.SetupReadyMatchesTerminal,
        _ => Strings.SetupReadyMatchesSystem,
    });

    public string TerminalSummary => Strings.Format(Strings.SetupReadyTerminal, s_terminalThemes[SelectedTerminalTheme] switch
    {
        TerminalTheme.Light => Strings.SetupReadyLight,
        TerminalTheme.Dark => Strings.SetupReadyDark,
        _ => Strings.SetupReadyMatchesSystem,
    });

    [ObservableProperty]
    public partial int SelectedWindowTheme { get; set; }

    [ObservableProperty]
    public partial int SelectedTerminalTheme { get; set; }

    // The colors a terminal will use, for the preview.
    public IBrush PreviewBackground => Brush(_settings.ActiveTheme(WindowColors.IsSystemDark).Background);

    public IBrush PreviewForeground => Brush(_settings.ActiveTheme(WindowColors.IsSystemDark).Foreground);

    private static SolidColorBrush Brush(int rgb) => new(Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb));

    public static ProviderAccount NewAccount(ProviderKind kind) => new()
    {
        Id = ProviderAccount.NewId(),
        Kind = kind,
        Name = ProviderCatalog.DefaultName(kind),
        Model = ProviderCatalog.DefaultModel(kind),
    };

    [RelayCommand]
    private void Next()
    {
        Problem = string.Empty;
        switch (Step)
        {
            case ProviderStep when !CheckProvider():
                return;
            case ProviderStep:
                HasProvider = true;
                break;
            case ReadyStep:
                Close();
                return;
        }

        Step++;
    }

    [RelayCommand]
    private void Back()
    {
        Problem = string.Empty;
        Step = Math.Max(WelcomeStep, Step - 1);
    }

    // On the provider step it sets only the provider aside; elsewhere, all that's left.
    [RelayCommand]
    private void Later()
    {
        Problem = string.Empty;
        if (Step == ProviderStep)
        {
            HasProvider = false;
            Step = AppearanceStep;
            return;
        }

        Close();
    }

    [RelayCommand(CanExecute = nameof(CanTest))]
    private async Task TestAsync()
    {
        Problem = string.Empty;
        if (!CheckProvider())
        {
            return;
        }

        IsTesting = true;
        TestFailed = false;
        TestResult = Strings.ProviderTesting;
        var key = KeyToUse();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var client = _settings.CreateClient(Account(), key);
            await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Reply with the single word OK.")], new ChatOptions { MaxOutputTokens = 1024 }, timeout.Token).ConfigureAwait(true);
            TestResult = Strings.ProviderTestPassed;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Error messages can echo what was sent; never show the key.
            var message = string.IsNullOrEmpty(key) ? exception.Message : exception.Message.Replace(key, "•••", StringComparison.Ordinal);
            TestResult = Strings.Format(Strings.ProviderTestFailed, message);
            TestFailed = true;
        }
        finally
        {
            IsTesting = false;
        }
    }

    private bool CanTest() => !IsTesting;

    // Finishing or setting setup aside: what was chosen is saved, and the window closes, unless the key couldn't be saved,
    // when setup goes back to the provider step to say why.
    public void Close()
    {
        if (!Finish(keepOpenOnFailure: true))
        {
            return;
        }

        Done?.Invoke(this, EventArgs.Empty);
    }

    // The window was closed: what was chosen is kept, and a key that couldn't be saved is dropped.
    public void Abandon() => Finish(keepOpenOnFailure: false);

    private bool Finish(bool keepOpenOnFailure)
    {
        if (_isClosed)
        {
            return true;
        }

        if (HasProvider && !SaveProvider())
        {
            HasProvider = false;
            if (keepOpenOnFailure)
            {
                Step = ProviderStep;
                return false;
            }
        }

        _isClosed = true;

        // Without a provider, a key in the environment is used as it was before setup existed.
        if (_settings.Providers.Count == 0 && !string.IsNullOrEmpty(_environment("ANTHROPIC_API_KEY")))
        {
            _settings.AddProvider(NewAccount(ProviderKind.Anthropic));
        }

        _settings.MarkSetupShown();
        return true;
    }

    private bool SaveProvider()
    {
        var account = Account();
        var key = Key.Trim();
        if (NeedsKey && key.Length > 0)
        {
            try
            {
                _settings.Keys.Save(account, key);
            }
            catch (CredentialStoreException exception)
            {
                Problem = exception.Message;
                return false;
            }
        }

        _settings.AddProvider(account);
        Key = string.Empty;
        return true;
    }

    private bool CheckProvider()
    {
        Problem = IsCompatible ? CheckServer() : CheckKey();
        return Problem.Length == 0;
    }

    private string CheckServer() =>
        Uri.TryCreate(Endpoint.Trim(), UriKind.Absolute, out var uri) && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)) && !string.IsNullOrWhiteSpace(Model)
            ? string.Empty
            : Strings.SetupNeedsServer;

    // A typed key needs somewhere to be kept; without one, a key in the environment will do.
    private string CheckKey()
    {
        if (!string.IsNullOrWhiteSpace(Key))
        {
            return _settings.Keys.CanSave ? string.Empty : KeyNote;
        }

        if (VariableName(Choice.Kind) is { } variable && !string.IsNullOrEmpty(_environment(variable)))
        {
            return string.Empty;
        }

        return _settings.Keys.CanSave ? Strings.SetupNeedsKey : KeyNote;
    }

    private string? KeyToUse() => !string.IsNullOrWhiteSpace(Key) ? Key.Trim() : VariableName(Choice.Kind) is { } variable ? _environment(variable) : null;

    // The account setup would add. Its id is fixed for this setup, so a key saved under it stays with it.
    private ProviderAccount Account() => _account with
    {
        Kind = Choice.Kind,
        Name = ProviderCatalog.DefaultName(Choice.Kind),
        Model = IsCompatible ? Model.Trim() : ProviderCatalog.DefaultModel(Choice.Kind),
        Endpoint = IsCompatible ? Endpoint.Trim() : null,
        IsLocal = IsCompatible && IsLocal,
    };

    private readonly ProviderAccount _account = NewAccount(ProviderKind.Anthropic);

    private static string? VariableName(ProviderKind kind) => kind switch
    {
        ProviderKind.Anthropic => "ANTHROPIC_API_KEY",
        ProviderKind.OpenAI => "OPENAI_API_KEY",
        _ => null,
    };

    partial void OnChoiceChanged(ProviderChoice value)
    {
        Key = string.Empty;
        Problem = string.Empty;
        TestResult = string.Empty;
    }

    partial void OnKeyChanged(string value) => ClearFeedback();

    partial void OnEndpointChanged(string value) => ClearFeedback();

    partial void OnModelChanged(string value) => ClearFeedback();

    private void ClearFeedback()
    {
        Problem = string.Empty;
        TestResult = string.Empty;
    }

    partial void OnSelectedWindowThemeChanged(int value)
    {
        if (_isLoading || !Enum.IsDefined((WindowTheme)value))
        {
            return;
        }

        _settings.SetWindowTheme((WindowTheme)value);
    }

    partial void OnSelectedTerminalThemeChanged(int value)
    {
        if (_isLoading || value < 0 || value >= s_terminalThemes.Length)
        {
            return;
        }

        _settings.UseTheme(s_terminalThemes[value]);
        OnPropertyChanged(nameof(PreviewBackground));
        OnPropertyChanged(nameof(PreviewForeground));
    }
}
