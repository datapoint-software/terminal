using System.Globalization;
using Avalonia.Data.Converters;

namespace Datapoint.Terminal.Components;

// Small labels set in capitals, such as a page's part names, without keeping a second, capitalized copy of each string.
public static class TextCase
{
    public static FuncValueConverter<string?, string?> Upper { get; } = new(text => text?.ToUpper(CultureInfo.CurrentCulture));
}
