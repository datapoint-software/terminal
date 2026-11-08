using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Agent.Providers;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Security.Credentials;
using Microsoft.Extensions.AI;

namespace Datapoint.Terminal.Settings;

// Every change is saved at once, as in macOS settings.
public sealed partial class ProviderViewModel : ObservableObject
{
    private readonly SettingsViewModel _owner;
    private readonly bool _isLoaded;

    public ProviderViewModel(SettingsViewModel owner, ProviderAccount account)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(account);
        _owner = owner;
        Id = account.Id;
        Kind = owner.Kinds.First(option => option.Kind == account.Kind);
        Name = account.Name;
        Model = account.Model;
        Endpoint = account.Endpoint ?? string.Empty;
        IsLocal = account.IsLocal;
        ContextWindow = Text(account.ContextWindow);
        OutputLimit = Text(account.OutputLimit);
        InputPrice = Text(account.InputPrice);
        OutputPrice = Text(account.OutputPrice);
        CachedInputPrice = Text(account.CachedInputPrice);
        RefreshKeyState();
        _isLoaded = true;
    }

    public string Id { get; }

    // A list or box whose items or text go away, as when Settings closes or shows another provider, sets its value to null
    // through the binding; the provider keeps its kind and model, which every save needs.
    public KindOption Kind
    {
        get;
        set
        {
            var old = field;
            if (value is null || !SetProperty(ref field, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsCompatible));
            OnPropertyChanged(nameof(NeedsKey));
            OnPropertyChanged(nameof(ModelChoices));
            NotifyModelChanged();
            OnPropertyChanged(nameof(Privacy));
            if (old is null)
            {
                return;
            }

            OnKindChanged(old, value);
        }
    } = null!;

    [ObservableProperty]
    public partial string Name { get; set; }

    public string Model
    {
        get;
        set
        {
            if (value is null || !SetProperty(ref field, value))
            {
                return;
            }

            NotifyModelChanged();
            Changed();
        }
    } = string.Empty;

    // Product decision 126: Anthropic and OpenAI models are chosen from the ones the app knows, whose context window and
    // prices come with them; Other… takes a model by name, with its own values. A compatible server's model is always typed.
    public IReadOnlyList<ModelOption> ModelChoices => IsCompatible
        ? []
        : [.. ProviderCatalog.Known.Where(model => model.Kind == Kind.Kind).Select(model => new ModelOption(model, model.Name)), s_other];

    private static readonly ModelOption s_other = new(null, Strings.ProviderModelOther);

    public ModelOption? SelectedModelChoice
    {
        get => IsCompatible ? null : Known is null ? ModelChoices[^1] : ModelChoices.First(option => option.Model == Known);
        set
        {
            // The list sets null when its items change, e.g. with the provider's kind.
            if (value is null || IsCompatible)
            {
                return;
            }

            // Other… starts from an empty name; a name the app knows, typed there, chooses that model.
            if (value.Model is { } model)
            {
                Model = model.Id;
            }
            else if (Known is not null)
            {
                Model = string.Empty;
            }

            NotifyModelChanged();
        }
    }

    public KnownModel? Known => ProviderCatalog.Find(Kind.Kind, Model);

    public bool IsKnownModel => Known is not null;

    public bool IsOtherModel => !IsCompatible && Known is null;

    public bool HasOwnValues => Known is null;

    public string KnownContextWindow => Known is { } known ? Strings.Format(Strings.ProviderTokens, known.ContextWindow) : string.Empty;

    public string KnownInputPrice => KnownPrice(price => price.InputPerMillion);

    public string KnownOutputPrice => KnownPrice(price => price.OutputPerMillion);

    public string KnownCachedInputPrice => KnownPrice(price => price.CachedInputPerMillion);

    public string KnownSource => Known is { } known
        ? Strings.Format(Strings.ProviderKnownSource, ProviderCatalog.DefaultName(known.Kind), ProviderCatalog.PricesAsOf.ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture))
        : string.Empty;

    private string KnownPrice(Func<Agent.Usage.TokenPrice, decimal?> rate)
    {
        if (Known?.Price is not { } price)
        {
            return string.Empty;
        }

        var text = Money(rate(price));
        return price.LongPrompt is { } longer && price.LongPromptAbove is { } above
            ? Strings.Format(Strings.ProviderLongPromptPrice, text, Money(rate(longer)), above)
            : text;
    }

    private static string Money(decimal? value) =>
        value is { } amount ? amount.ToString("0.00##", System.Globalization.CultureInfo.CurrentCulture) : Strings.ProviderNoPrice;

    private void NotifyModelChanged()
    {
        OnPropertyChanged(nameof(SelectedModelChoice));
        OnPropertyChanged(nameof(Known));
        OnPropertyChanged(nameof(IsKnownModel));
        OnPropertyChanged(nameof(IsOtherModel));
        OnPropertyChanged(nameof(HasOwnValues));
        OnPropertyChanged(nameof(KnownContextWindow));
        OnPropertyChanged(nameof(KnownInputPrice));
        OnPropertyChanged(nameof(KnownOutputPrice));
        OnPropertyChanged(nameof(KnownCachedInputPrice));
        OnPropertyChanged(nameof(KnownSource));
        OnPropertyChanged(nameof(OutputLimitHint));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Privacy))]
    public partial string Endpoint { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Privacy))]
    public partial bool IsLocal { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputLimitHint))]
    public partial string ContextWindow { get; set; }

    [ObservableProperty]
    public partial string OutputLimit { get; set; }

    [ObservableProperty]
    public partial string InputPrice { get; set; }

    [ObservableProperty]
    public partial string OutputPrice { get; set; }

    [ObservableProperty]
    public partial string CachedInputPrice { get; set; }

    // A stored number in the user's own number format, or empty.
    private static string Text<T>(T? value)
        where T : struct, IFormattable => value?.ToString(null, System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;

    // In the user's own number format, as typed.
    internal static decimal? Price(string text) =>
        decimal.TryParse(text.Trim(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.CurrentCulture, out var value) && value >= 0
            ? value
            : null;

    public string OutputLimitHint => Strings.Format(Strings.ProviderOutputLimitHint, OutputLimits.For(Known?.ContextWindow ?? Number(ContextWindow), null));

    private static int? Number(string text) =>
        int.TryParse(text.Replace(",", string.Empty, StringComparison.Ordinal).Replace(".", string.Empty, StringComparison.Ordinal).Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : null;

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
    public partial string TestResult { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool TestFailed { get; private set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(TestCommand))]
    public partial bool IsTesting { get; private set; }

    public bool IsActive => string.Equals(_owner.SelectedProviderId, Id, StringComparison.Ordinal);

    public bool IsCompatible => Kind.Kind == ProviderKind.OpenAICompatible;

    public bool NeedsKey => ProviderCatalog.NeedsKey(Kind.Kind);

    public bool CanSaveKeys => _owner.Keys.CanSave;

    public bool ShowKeyEntry => CanSaveKeys && (!HasSavedKey || IsReplacingKey);

    public string Privacy => ProviderPrivacy.Describe(ToAccount());

    public ProviderAccount ToAccount() => new()
    {
        Id = Id,
        Kind = Kind.Kind,
        Name = string.IsNullOrWhiteSpace(Name) ? ProviderCatalog.DefaultName(Kind.Kind) : Name.Trim(),
        Model = Model.Trim(),
        Endpoint = IsCompatible && !string.IsNullOrWhiteSpace(Endpoint) ? Endpoint.Trim() : null,
        IsLocal = IsCompatible && IsLocal,
        ContextWindow = Number(ContextWindow),
        OutputLimit = Number(OutputLimit),
        InputPrice = Price(InputPrice),
        OutputPrice = Price(OutputPrice),
        CachedInputPrice = Price(CachedInputPrice),
    };

    internal void NotifyActiveChanged() => OnPropertyChanged(nameof(IsActive));

    private void OnKindChanged(KindOption oldValue, KindOption newValue)
    {
        if (!_isLoaded)
        {
            return;
        }

        // Follow the new kind unless the user chose their own name or model.
        if (string.IsNullOrWhiteSpace(Name) || string.Equals(Name, ProviderCatalog.DefaultName(oldValue.Kind), StringComparison.Ordinal))
        {
            Name = ProviderCatalog.DefaultName(newValue.Kind);
        }

        if (string.IsNullOrWhiteSpace(Model) || ProviderCatalog.Models(oldValue.Kind).Contains(Model, StringComparer.Ordinal))
        {
            Model = ProviderCatalog.DefaultModel(newValue.Kind);
        }

        RefreshKeyState();
        Changed();
    }

    partial void OnNameChanged(string value) => Changed();

    partial void OnEndpointChanged(string value) => Changed();

    partial void OnIsLocalChanged(bool value) => Changed();

    partial void OnContextWindowChanged(string value) => Changed();

    partial void OnOutputLimitChanged(string value) => Changed();

    partial void OnInputPriceChanged(string value) => Changed();

    partial void OnOutputPriceChanged(string value) => Changed();

    partial void OnCachedInputPriceChanged(string value) => Changed();

    [RelayCommand(CanExecute = nameof(CanSaveKey))]
    private void SaveKey()
    {
        try
        {
            _owner.Keys.Save(ToAccount(), KeyDraft);
            KeyDraft = string.Empty;
            IsReplacingKey = false;
            TestResult = string.Empty;
            RefreshKeyState();
            _owner.NotifyChanged();
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
            _owner.Keys.Delete(ToAccount());
            IsReplacingKey = false;
            RefreshKeyState();
            _owner.NotifyChanged();
        }
        catch (CredentialStoreException exception)
        {
            KeyStatus = exception.Message;
        }
    }

    [RelayCommand]
    private void Use() => _owner.Activate(this);

    [RelayCommand(CanExecute = nameof(CanTest))]
    private async Task TestAsync()
    {
        IsTesting = true;
        TestFailed = false;
        TestResult = Strings.ProviderTesting;
        string? key = null;
        try
        {
            key = _owner.Keys.Read(ToAccount());
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var client = _owner.CreateClient(ToAccount(), key);
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

    private bool CanSaveKey() => !string.IsNullOrWhiteSpace(KeyDraft);

    private bool CanTest() => !IsTesting;

    private void RefreshKeyState()
    {
        var account = ToAccount();
        var keys = _owner.Keys;
        try
        {
            HasSavedKey = keys.HasSavedKey(account);
            var variable = keys.EnvironmentVariableFor(account);
            KeyStatus = (HasSavedKey, variable, keys.CanSave) switch
            {
                (true, _, _) => Strings.Format(Strings.ProviderKeySaved, keys.StoreName),
                (false, { } name, true) => Strings.Format(Strings.ProviderKeyFromEnvironment, name, keys.StoreName),
                (false, _, false) => Strings.Format(Strings.ProviderKeyNoStore, Kind.Kind == ProviderKind.OpenAI ? "OPENAI_API_KEY" : "ANTHROPIC_API_KEY"),
                _ => NeedsKey ? Strings.ProviderKeyGetOne : Strings.ProviderApiKeyOptional,
            };
        }
        catch (CredentialStoreException exception)
        {
            HasSavedKey = false;
            KeyStatus = exception.Message;
        }
    }

    private void Changed()
    {
        if (!_isLoaded)
        {
            return;
        }

        _owner.Save();
    }
}
