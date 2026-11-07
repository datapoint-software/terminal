namespace Datapoint.Terminal.Editor;

// Body is the block as written; Trailing is the line break and any blank lines after it, kept as they were.
public sealed record MarkdownBlock(string Body, string Trailing, BlockKind Kind);
