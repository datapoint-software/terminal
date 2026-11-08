namespace Datapoint.Terminal.Resources;

// An item in a list of choices whose text follows the interface's language (product decision 150). The item stays the
// same object when the language changes, so the list keeps what's chosen; App.axaml's template shows its text.
public sealed class TextChoice : LocalizedObject
{
    private readonly Func<string> _text;

    public TextChoice(Func<string> text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _text = text;
    }

    public string Text => _text();

    public override string ToString() => Text;
}
