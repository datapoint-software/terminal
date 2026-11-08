using System.Globalization;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Datapoint.Terminal.Resources;

// {res:Tr Key}: a resource's text that follows the interface's language (product decision 150); x:Static would read it
// only once. With Argument, the text is a format and the binding fills its {0}: {res:Tr SidebarAddHost, Argument={Binding Alias}}.
// ResourceKeysTests checks every key used this way exists.
public sealed class TrExtension(string key)
{
    public string Key { get; } = key;

    // For headings set in capitals, by the language's own rules.
    public bool Upper { get; set; }

    public BindingBase? Argument { get; set; }

    public BindingBase ProvideValue(IServiceProvider serviceProvider)
    {
        var text = Localizer.Instance.Observe(Key, Upper).ToBinding();
        if (Argument is null)
        {
            return text;
        }

        return new MultiBinding { Bindings = { text, Argument }, Converter = FormatConverter.Instance };
    }

    private sealed class FormatConverter : IMultiValueConverter
    {
        public static FormatConverter Instance { get; } = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
            values is [string format, var argument] ? Strings.Format(format, argument) : null;
    }
}
