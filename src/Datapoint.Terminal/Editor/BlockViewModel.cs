using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Datapoint.Terminal.Editor;

// One block of the document: shown rendered, or as its Markdown while the user edits it, at the same size so the page barely
// moves.
public sealed partial class BlockViewModel : ObservableObject
{
    public BlockViewModel(MarkdownBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        Body = block.Body;
        Trailing = block.Trailing;
        Kind = block.Kind;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    public partial string Body { get; set; }

    public string Trailing { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShown), nameof(FontSize), nameof(FontWeight), nameof(IsCode), nameof(AcceptsReturn), nameof(IsListItem))]
    public partial BlockKind Kind { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRendered))]
    public partial bool IsEditing { get; set; }

    public bool IsRendered => !IsEditing;

    public bool IsShown => Kind is not (BlockKind.Gap or BlockKind.Hidden);

    public bool IsEmpty => Body.Length == 0;

    public bool IsCode => Kind == BlockKind.Code;

    public bool IsListItem => Kind is BlockKind.Bullet or BlockKind.Numbered or BlockKind.Task;

    // In code, tables, quotes and anything unusual, Enter starts a new line within the block; elsewhere it starts a new block.
    public bool AcceptsReturn => Kind is BlockKind.Code or BlockKind.Table or BlockKind.Quote or BlockKind.Other;

    // The space above the block: items of one list sit close together, as in the rendered list.
    [ObservableProperty]
    public partial Thickness Spacing { get; set; }

    public double FontSize => Kind switch
    {
        BlockKind.Heading1 => DocumentType.Heading1,
        BlockKind.Heading2 => DocumentType.Heading2,
        BlockKind.Heading3 => DocumentType.Heading3,
        BlockKind.Code => DocumentType.Code,
        _ => DocumentType.Body,
    };

    public FontWeight FontWeight => Kind is BlockKind.Heading1 or BlockKind.Heading2 or BlockKind.Heading3 ? FontWeight.SemiBold : FontWeight.Normal;

    public string Text => Body + Trailing;
}
