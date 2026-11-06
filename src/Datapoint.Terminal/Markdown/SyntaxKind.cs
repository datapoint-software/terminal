namespace Datapoint.Terminal.Markdown;

/// <summary>What a piece of code is, for colouring it. Each kind but <see cref="Plain"/> has a <c>Syntax…Brush</c> resource.</summary>
internal enum SyntaxKind
{
    Plain,
    Keyword,
    String,
    Comment,
    Number,
    Variable,
    Added,
    Removed,
}
