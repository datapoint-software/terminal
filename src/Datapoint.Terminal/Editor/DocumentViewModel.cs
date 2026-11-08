using System.Collections.ObjectModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Datapoint.Terminal.Editor;

// A Markdown file open in the editor (product decision 98): blocks shown rendered, one edited at a time as Markdown, saved as
// the user types. The window decides when a save happens; this says when one is due.
public sealed partial class DocumentViewModel : Resources.LocalizedObject, IOpenDocument
{
    private const double BlockGap = 12;
    private const double ListGap = 3;

    private readonly MarkdownFile _file;
    private BlockViewModel? _editing;
    private string _editingStart = string.Empty;

    // A plain text file opens as text only, with nothing rendered.
    public DocumentViewModel(MarkdownFile file, bool isPlainText = false)
    {
        ArgumentNullException.ThrowIfNull(file);
        _file = file;
        IsPlainText = isPlainText;
        if (isPlainText)
        {
            SourceText = file.Text;
            IsSourceMode = true;
            return;
        }

        Load(file.Text);
    }

    public bool IsPlainText { get; }

    public BlockViewModel? EditingBlock => _editing;

    public bool CanShowMarkdown => !IsPlainText;

    public string Path => _file.Path;

    // A skill's YAML header, kept out of the editor; the skill's form changes it.
    public string Header => _file.Header;

    public SaveResult SaveHeader(string header) => _file.SaveHeader(header);

    // What the tab calls the document: its own title, e.g. a host's name for its README.md, or else the file's name.
    public string? Title { get; init; }

    public string Name => Title ?? System.IO.Path.GetFileName(Path);

    public string ToolTip => Path;

    // The space below the last block that starts a new paragraph; shorter above a host's log, so the log stays near.
    public double EndAreaHeight => Host is null ? 160 : 48;

    // Saved as the user types.
    public bool IsUnsaved => false;

    public ObservableCollection<BlockViewModel> Blocks { get; } = [];

    public bool IsEmpty => !Blocks.Any(block => block.IsShown);

    public string Text => IsSourceMode ? SourceText : string.Concat(Blocks.Select(block => block.Text));

    public string AccessibleName => Resources.Strings.Format(IsDirty ? Resources.Strings.DocumentTabUnsaved : Resources.Strings.DocumentTabName, Name);

    public string CloseName => Resources.Strings.Format(Resources.Strings.DocumentClose, Name);

    // Called when the user closes the document's tab.
    public Action<DocumentViewModel>? CloseRequested { get; set; }

    // Called when a change is waiting to be saved; the window saves a moment later, so typing isn't slowed by the disk.
    public Action? SaveDue { get; set; }

    // Called when the editor needs to move the caret: the block, and where in its text.
    public Action<BlockViewModel, int>? FocusRequested { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibleName))]
    public partial bool IsDirty { get; private set; }

    // The file changed on disk, e.g. the agent updated it, while the user had changes not yet saved.
    [ObservableProperty]
    public partial bool HasConflict { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    public partial string? Problem { get; private set; }

    public bool HasProblem => Problem is not null;

    // Show Markdown: the whole file as text, for the times the user wants all of it at once.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRichMode))]
    public partial bool IsSourceMode { get; set; }

    public bool IsRichMode => !IsSourceMode;

    [ObservableProperty]
    public partial string SourceText { get; set; } = string.Empty;

    // The host this document describes, if it's one in hosts/: shown above it.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHost), nameof(EndAreaHeight))]
    public partial HostHeaderViewModel? Host { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSkill))]
    public partial SkillHeaderViewModel? Skill { get; set; }

    public bool HasSkill => Skill is not null;

    public bool HasHost => Host is not null;

    partial void OnIsSourceModeChanging(bool value)
    {
        if (!value)
        {
            return;
        }

        // Read before the mode changes, while Text still comes from the blocks.
        Commit();
        SourceText = Text;
    }

    partial void OnIsSourceModeChanged(bool value)
    {
        if (value)
        {
            return;
        }

        Load(SourceText);
    }

    // Typing in the whole source is only read into blocks on going back, so a long file isn't parsed at every key.
    partial void OnSourceTextChanged(string value)
    {
        if (!IsSourceMode || string.Equals(value, _file.Text, StringComparison.Ordinal) && !IsDirty)
        {
            return;
        }

        Changed();
    }

    public void Edit(BlockViewModel block, int? caret = null)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (!ReferenceEquals(_editing, block))
        {
            Commit();
            _editing = block;
            _editingStart = block.Body;
            block.IsEditing = true;
        }

        FocusRequested?.Invoke(block, Math.Clamp(caret ?? block.Body.Length, 0, block.Body.Length));
    }

    // The block being edited is done: it's read again as Markdown, so text typed into it can become a heading, a list or several
    // blocks, and an emptied block goes away.
    public void Commit()
    {
        if (_editing is not { } block)
        {
            return;
        }

        _editing = null;
        block.IsEditing = false;
        if (string.Equals(block.Body, _editingStart, StringComparison.Ordinal))
        {
            return;
        }

        var index = Blocks.IndexOf(block);
        if (index < 0)
        {
            return;
        }

        Blocks.RemoveAt(index);
        var parts = MarkdownBlocks.Split(block.Text).Select(part => new BlockViewModel(part)).ToList();
        if (parts.Count == 0 && index > 0)
        {
            // Nothing left: the blank lines go with the block before, so the rest of the file keeps its shape.
            Blocks[index - 1].Trailing = MaxGap(Blocks[index - 1].Trailing, block.Trailing);
        }

        for (var offset = 0; offset < parts.Count; offset++)
        {
            Blocks.Insert(index + offset, parts[offset]);
        }

        Arrange();
    }

    // The block's size follows what's typed, so "# " makes a heading straight away.
    public void OnBodyEdited(BlockViewModel block)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (block.Kind is not (BlockKind.Code or BlockKind.Table or BlockKind.Other))
        {
            block.Kind = block.Body.Length == 0 ? BlockKind.Paragraph : MarkdownBlocks.KindOf(block.Body);
        }

        Changed();
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this);

    // A block for the user's first words in an empty document, or at the end of one.
    public void AddAtEnd()
    {
        if (Blocks.LastOrDefault(block => block.IsShown) is { IsEmpty: true, IsListItem: false } last)
        {
            Edit(last);
            return;
        }

        if (Blocks.Count > 0 && !Blocks[^1].Trailing.EndsWith("\n\n", StringComparison.Ordinal))
        {
            Blocks[^1].Trailing = Blocks[^1].Trailing.EndsWith('\n') ? Blocks[^1].Trailing + "\n" : Blocks[^1].Trailing + "\n\n";
        }

        var block = new BlockViewModel(new MarkdownBlock(string.Empty, "\n", BlockKind.Paragraph));
        Blocks.Add(block);
        Arrange();
        Edit(block);
    }

    // Enter: the text after the caret starts the next block, as the next item of a list.
    public void Split(BlockViewModel block, int caret)
    {
        ArgumentNullException.ThrowIfNull(block);
        var index = Blocks.IndexOf(block);
        if (index < 0)
        {
            return;
        }

        var marker = block.IsListItem ? ListMarker.Of(block.Body) : null;
        if (marker is not null && block.Body.Trim().Length == marker.Trim().Length)
        {
            EndList(block, index);
            return;
        }

        caret = Math.Clamp(caret, 0, block.Body.Length);
        var prefix = marker is null ? string.Empty : ListMarker.Next(marker);
        var next = new BlockViewModel(new MarkdownBlock(prefix + block.Body[caret..].TrimStart(' '), block.Trailing, marker is null ? BlockKind.Paragraph : block.Kind));
        block.Body = block.Body[..caret].TrimEnd(' ');
        block.Trailing = marker is null ? "\n\n" : "\n";
        _editingStart = block.Body + "\0";
        Blocks.Insert(index + 1, next);
        Arrange();
        Changed();
        Edit(next, prefix.Length);
    }

    // Enter on an empty item ends the list, with a blank line so what's typed next isn't read as part of the last item.
    private void EndList(BlockViewModel block, int index)
    {
        block.Body = string.Empty;
        block.Kind = BlockKind.Paragraph;
        if (index > 0 && !Blocks[index - 1].Trailing.EndsWith("\n\n", StringComparison.Ordinal))
        {
            Blocks[index - 1].Trailing = Blocks[index - 1].Trailing.TrimEnd('\n') + "\n\n";
        }

        Arrange();
        Changed();
        FocusRequested?.Invoke(block, 0);
    }

    // Backspace at the start: the block joins the one before, unless that one is code or a table, which only takes the caret.
    public void JoinWithPrevious(BlockViewModel block)
    {
        ArgumentNullException.ThrowIfNull(block);
        if (Previous(block) is not { } previous)
        {
            return;
        }

        if (previous.Kind is BlockKind.Code or BlockKind.Table)
        {
            Edit(previous);
            return;
        }

        var caret = previous.Body.Length;
        var text = block.IsListItem && ListMarker.Of(block.Body) is { } marker ? block.Body[marker.Length..] : block.Body;
        _editing = null;
        block.IsEditing = false;
        previous.Body += text;
        previous.Trailing = block.Trailing;
        Blocks.Remove(block);
        Arrange();
        Changed();
        Edit(previous, caret);
    }

    public void MoveToPrevious(BlockViewModel block)
    {
        if (Previous(block) is not { } previous)
        {
            return;
        }

        Edit(previous);
    }

    public void MoveToNext(BlockViewModel block)
    {
        if (Next(block) is not { } next)
        {
            return;
        }

        Edit(next, 0);
    }

    // Saves now; false when the file changed on disk meanwhile, which the user is then asked about.
    public bool Save()
    {
        if (!IsDirty)
        {
            return true;
        }

        try
        {
            if (_file.Save(Text) == SaveResult.Conflict)
            {
                HasConflict = true;
                return false;
            }

            IsDirty = false;
            Problem = null;
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Problem = Resources.Strings.Format(Resources.Strings.DocumentSaveFailed, exception.Message);
            return false;
        }
    }

    // The file changed on disk: taken as is when there's nothing unsaved, otherwise the user chooses.
    public void OnChangedOnDisk()
    {
        if (_file.ChangedOnDisk() is null)
        {
            return;
        }

        if (IsDirty || (_editing is { } block && !string.Equals(block.Body, _editingStart, StringComparison.Ordinal)))
        {
            HasConflict = true;
            return;
        }

        TakeTheirs();
    }

    [RelayCommand]
    private void KeepMine()
    {
        HasConflict = false;
        Commit();
        _file.Save(Text, overwrite: true);
        IsDirty = false;
    }

    [RelayCommand]
    private void TakeTheirs()
    {
        HasConflict = false;
        _editing = null;
        _file.Reload();
        IsDirty = false;
        if (IsSourceMode)
        {
            SourceText = _file.Text;
            return;
        }

        Load(_file.Text);
    }

    private void Changed()
    {
        IsDirty = true;
        OnPropertyChanged(nameof(IsEmpty));
        SaveDue?.Invoke();
    }

    private void Load(string text)
    {
        _editing = null;
        Blocks.Clear();
        foreach (var block in MarkdownBlocks.Split(text))
        {
            Blocks.Add(new BlockViewModel(block));
        }

        Arrange();
    }

    private void Arrange()
    {
        BlockViewModel? before = null;
        foreach (var block in Blocks.Where(block => block.IsShown))
        {
            var tight = before is { IsListItem: true } && block.IsListItem && !before.Trailing.Contains("\n\n", StringComparison.Ordinal);
            block.Spacing = new Thickness(0, before is null ? 0 : tight ? ListGap : BlockGap, 0, 0);
            before = block;
        }

        OnPropertyChanged(nameof(IsEmpty));
    }

    private BlockViewModel? Previous(BlockViewModel block) => Blocks.Take(Math.Max(Blocks.IndexOf(block), 0)).LastOrDefault(item => item.IsShown);

    private BlockViewModel? Next(BlockViewModel block) => Blocks.Skip(Blocks.IndexOf(block) + 1).FirstOrDefault(item => item.IsShown);

    private static string MaxGap(string first, string second) => first.Length >= second.Length ? first : second;
}
