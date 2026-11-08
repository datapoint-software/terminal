using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Security.Credentials;

namespace Datapoint.Terminal.Settings;

// Settings › Web (product decision 105): whether the agent asks before using the web, and who runs its searches.
public sealed partial class WebViewModel : LocalizedObject
{
    private static readonly WebSearchService[] s_services =
        [WebSearchService.Provider, WebSearchService.Brave, WebSearchService.Tavily, WebSearchService.Searxng, WebSearchService.Off];

    private readonly SettingsViewModel _settings;
    private bool _isLoading;

    public WebViewModel(SettingsViewModel settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        settings.Changed += (_, _) => Load();
        Load();
    }

    public IReadOnlyList<TextChoice> Services { get; } =
        [new(() => Strings.WebServiceProvider), new(() => Strings.WebServiceBrave), new(() => Strings.WebServiceTavily), new(() => Strings.WebServiceSearxng), new(() => Strings.WebServiceOff)];

    [ObservableProperty]
    public partial bool AlwaysAllow { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Hint), nameof(NeedsKey), nameof(NeedsServer), nameof(ShowKeyEntry))]
    public partial int SelectedService { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveKeyCommand))]
    public partial string KeyDraft { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowKeyEntry))]
    public partial bool HasSavedKey { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowKeyEntry))]
    public partial bool IsReplacingKey { get; private set; }

    [ObservableProperty]
    public partial string KeyStatus { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial string Server { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ServerStatus { get; private set; } = string.Empty;

    private WebSearchService Service => s_services[Math.Clamp(SelectedService, 0, s_services.Length - 1)];

    public bool NeedsKey => Service is WebSearchService.Brave or WebSearchService.Tavily;

    public bool NeedsServer => Service == WebSearchService.Searxng;

    public bool ShowKeyEntry => NeedsKey && _settings.WebKeys.CanSave && (!HasSavedKey || IsReplacingKey);

    public string Hint => Service switch
    {
        WebSearchService.Brave => Strings.WebHintBrave,
        WebSearchService.Tavily => Strings.WebHintTavily,
        WebSearchService.Searxng => Strings.WebHintSearxng,
        WebSearchService.Off => Strings.WebHintOff,
        _ => Strings.WebHintProvider,
    };

    private void Load()
    {
        _isLoading = true;
        AlwaysAllow = _settings.AlwaysAllowWeb;
        SelectedService = Math.Max(0, Array.IndexOf(s_services, _settings.WebSearch));
        Server = _settings.SearxngServer ?? string.Empty;
        _isLoading = false;
        RefreshKeyState();
    }

    partial void OnAlwaysAllowChanged(bool value)
    {
        if (_isLoading)
        {
            return;
        }

        _settings.SetAlwaysAllowWeb(value);
    }

    partial void OnSelectedServiceChanged(int value)
    {
        if (_isLoading)
        {
            return;
        }

        IsReplacingKey = false;
        KeyDraft = string.Empty;
        _settings.SetWebSearch(Service, _settings.SearxngServer);
    }

    [RelayCommand]
    private void SaveServer()
    {
        var text = Server.Trim();
        if (text.Length > 0 && !(Uri.TryCreate(text, UriKind.Absolute, out var address) && Agent.Web.WebAddressGuard.IsWebScheme(address.Scheme)))
        {
            ServerStatus = Strings.WebServerInvalid;
            return;
        }

        ServerStatus = string.Empty;
        _settings.SetWebSearch(Service, text);
    }

    [RelayCommand(CanExecute = nameof(CanSaveKey))]
    private void SaveKey()
    {
        try
        {
            _settings.WebKeys.Save(Service, KeyDraft);
            KeyDraft = string.Empty;
            IsReplacingKey = false;
            _settings.NotifyChanged();
        }
        catch (CredentialStoreException exception)
        {
            KeyStatus = exception.Message;
        }
    }

    [RelayCommand]
    private void ReplaceKey() => IsReplacingKey = true;

    [RelayCommand]
    private void RemoveKey()
    {
        try
        {
            _settings.WebKeys.Delete(Service);
            IsReplacingKey = false;
            _settings.NotifyChanged();
        }
        catch (CredentialStoreException exception)
        {
            KeyStatus = exception.Message;
        }
    }

    private bool CanSaveKey() => !string.IsNullOrWhiteSpace(KeyDraft);

    private void RefreshKeyState()
    {
        var keys = _settings.WebKeys;
        try
        {
            HasSavedKey = NeedsKey && keys.HasSavedKey(Service);
            KeyStatus = (NeedsKey, HasSavedKey, keys.EnvironmentVariableFor(Service), keys.CanSave) switch
            {
                (false, _, _, _) => string.Empty,
                (_, true, _, _) => Strings.Format(Strings.ProviderKeySaved, keys.StoreName),
                (_, _, { } name, true) => Strings.Format(Strings.ProviderKeyFromEnvironment, name, keys.StoreName),
                (_, _, _, false) => Strings.Format(Strings.ProviderKeyNoStore, WebSearchKeys.VariableName(Service)),
                _ => string.Empty,
            };
        }
        catch (CredentialStoreException exception)
        {
            HasSavedKey = false;
            KeyStatus = exception.Message;
        }
    }
}
