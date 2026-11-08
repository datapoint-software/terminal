using System.Globalization;

namespace Datapoint.Terminal.Resources;

// Product decision 150. Holds the interface's language and tells whatever shows text when it changes, so switching
// takes effect at once, without a restart. Views bind through TrExtension; code that builds text registers an
// ILocalizable. Both are held weakly: a view or view model that's gone isn't kept alive by being told about languages.
// Only the interface's language changes: dates and numbers keep following the system's regional format.
public sealed class Localizer
{
    private static readonly CultureInfo s_english = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo s_portuguese = CultureInfo.GetCultureInfo("pt-PT");

    private readonly List<WeakReference<ILocalizable>> _targets = [];
    private readonly Lock _lock = new();
    private int _pruneAt = 64;

    private Localizer(CultureInfo systemCulture)
    {
        SystemCulture = systemCulture;
        Culture = Resolve(AppLanguage.System, systemCulture);
    }

    // Every language we have a catalog for.
    public static IReadOnlyList<CultureInfo> Cultures { get; } = [s_english, s_portuguese];

    public static Localizer Instance { get; } = new(CultureInfo.CurrentUICulture);

    // The operating system's language, as it was when the application started.
    public CultureInfo SystemCulture { get; }

    public AppLanguage Language { get; private set; }

    public CultureInfo Culture { get; private set; }

    public static CultureInfo Resolve(AppLanguage language, CultureInfo systemCulture)
    {
        ArgumentNullException.ThrowIfNull(systemCulture);
        return language switch
        {
            AppLanguage.English => s_english,
            AppLanguage.PortuguesePortugal => s_portuguese,
            // Any Portuguese, Brazil's included, gets Portugal's: it's the only one we have.
            _ => string.Equals(systemCulture.TwoLetterISOLanguageName, "pt", StringComparison.OrdinalIgnoreCase) ? s_portuguese : s_english,
        };
    }

    public void Use(AppLanguage language)
    {
        var culture = Resolve(language, SystemCulture);
        Language = language;
        if (culture.Equals(Culture))
        {
            return;
        }

        Culture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
        foreach (var target in LiveTargets())
        {
            target.OnLanguageChanged();
        }
    }

    public void Register(ILocalizable target)
    {
        ArgumentNullException.ThrowIfNull(target);
        lock (_lock)
        {
            // Pruned only when the list has doubled, so registering stays cheap however many there are.
            if (_targets.Count >= _pruneAt)
            {
                _targets.RemoveAll(reference => !reference.TryGetTarget(out _));
                _pruneAt = Math.Max(64, _targets.Count * 2);
            }

            _targets.Add(new WeakReference<ILocalizable>(target));
        }
    }

    // The text for a resource key, now and after every change of language.
    public IObservable<string> Observe(string key, bool upper = false) => new LocalizedText(this, key, upper);

    private List<ILocalizable> LiveTargets()
    {
        lock (_lock)
        {
            var live = new List<ILocalizable>(_targets.Count);
            foreach (var reference in _targets)
            {
                if (reference.TryGetTarget(out var target))
                {
                    live.Add(target);
                }
            }

            return live;
        }
    }

    private sealed class LocalizedText(Localizer localizer, string key, bool upper) : IObservable<string>
    {
        public IDisposable Subscribe(IObserver<string> observer)
        {
            ArgumentNullException.ThrowIfNull(observer);
            var subscription = new Subscription(observer, key, upper);
            localizer.Register(subscription);
            subscription.OnLanguageChanged();
            return subscription;
        }
    }

    // Held weakly by the localizer and strongly by the binding that subscribed, so it lives as long as the binding.
    private sealed class Subscription(IObserver<string> observer, string key, bool upper) : ILocalizable, IDisposable
    {
        private bool _isDisposed;

        public void OnLanguageChanged()
        {
            if (_isDisposed)
            {
                return;
            }

            var text = Strings.Get(key);
            observer.OnNext(upper ? text.ToUpper(Instance.Culture) : text);
        }

        public void Dispose() => _isDisposed = true;
    }
}
