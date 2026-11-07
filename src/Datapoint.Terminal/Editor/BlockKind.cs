namespace Datapoint.Terminal.Editor;

public enum BlockKind
{
    Paragraph,
    Heading1,
    Heading2,
    Heading3,
    Bullet,
    Numbered,
    Task,
    Quote,
    Code,
    Table,
    Other,

    // Kept in the file but not shown: blank lines before the first block, and comments.
    Gap,
    Hidden,
}
