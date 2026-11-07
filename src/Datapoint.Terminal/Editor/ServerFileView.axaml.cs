using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using Datapoint.Terminal.Markdown;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Editor;

public sealed partial class ServerFileView : UserControl
{
    private readonly SyntaxColorizer _colorizer;
    private readonly DispatcherTimer _colorTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private ServerFileViewModel? _file;
    private bool _isLoading;
    private int _colorVersion;

    public ServerFileView()
    {
        InitializeComponent();
        _colorizer = new SyntaxColorizer(BrushFor);
        Editor.TextArea.TextView.LineTransformers.Add(_colorizer);
        Editor.Options.HighlightCurrentLine = true;
        Editor.Options.EnableHyperlinks = false;
        Editor.Options.EnableEmailHyperlinks = false;
        Editor.Options.ConvertTabsToSpaces = false;
        Editor.TextArea.TextView.CurrentLineBorder = new Pen(Brushes.Transparent, 0);
        Editor.TextArea.TextView.Margin = new Thickness(10, 8, 0, 0);
        foreach (var margin in Editor.TextArea.LeftMargins)
        {
            // Room on both sides of the line numbers, and no dotted rule after them: the colour change is divider enough.
            margin.Margin = margin is AvaloniaEdit.Editing.LineNumberMargin ? new Thickness(14, 8, 0, 0) : margin.Margin;
            margin.IsVisible = margin is AvaloniaEdit.Editing.LineNumberMargin;
        }

        Editor.TextChanged += (_, _) => OnEdited();
        Editor.TextArea.Caret.PositionChanged += (_, _) => ShowCaret();
        _colorTimer.Tick += (_, _) => _ = ColourAsync();
        ActualThemeVariantChanged += (_, _) => ApplyColours();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    public void FocusText() => Editor.TextArea.Focus();

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _file?.PropertyChanged -= OnFilePropertyChanged;
        _file = DataContext as ServerFileViewModel;
        _file?.PropertyChanged += OnFilePropertyChanged;
        Load();
    }

    private void Load()
    {
        _isLoading = true;
        Editor.Document = new TextDocument(_file?.Text ?? string.Empty);
        _isLoading = false;
        ApplyColours();
        _ = ColourAsync();
    }

    private void OnFilePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Revert and reload change the text from outside the editor.
        if (!string.Equals(e.PropertyName, nameof(ServerFileViewModel.Text), StringComparison.Ordinal) || _file is null || string.Equals(Editor.Text, _file.Text, StringComparison.Ordinal))
        {
            return;
        }

        _isLoading = true;
        Editor.Document.Text = _file.Text;
        _isLoading = false;
        _ = ColourAsync();
    }

    private void OnEdited()
    {
        if (_isLoading || _file is null)
        {
            return;
        }

        _file.Text = Editor.Text;
        _colorTimer.Stop();
        _colorTimer.Start();
    }

    private void ShowCaret()
    {
        if (_file is null)
        {
            return;
        }

        var caret = Editor.TextArea.Caret;
        _file.Caret = Strings.Format(Strings.HostFileCaret, caret.Line, caret.Column);
    }

    // Worked out away from the window's thread: a large file takes a moment, and typing shouldn't wait for it.
    private async Task ColourAsync()
    {
        _colorTimer.Stop();
        var version = ++_colorVersion;
        var text = Editor.Text;
        var language = _file?.Language?.Name;
        var spans = await Task.Run(() => SyntaxColorizer.Spans(text, language)).ConfigureAwait(true);
        if (version != _colorVersion)
        {
            return;
        }

        _colorizer.SetSpans(spans);
        Editor.TextArea.TextView.Redraw();
    }

    private void ApplyColours()
    {
        Editor.TextArea.TextView.CurrentLineBackground = Resource("AccentTintBrush");
        Editor.TextArea.SelectionBrush = Resource("AccentTintBrush") is ISolidColorBrush tint ? new SolidColorBrush(tint.Color, 1) : null;
        Editor.TextArea.TextView.Redraw();
    }

    private IBrush? BrushFor(SyntaxKind kind) => Resource($"Syntax{kind}Brush");

    private IBrush? Resource(string key) => this.TryFindResource(key, ActualThemeVariant, out var value) ? value as IBrush : null;

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var command = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        if (e.Key != Key.S || e.KeyModifiers != command || _file is null)
        {
            return;
        }

        e.Handled = true;
        if (!_file.SaveCommand.CanExecute(null))
        {
            return;
        }

        _file.SaveCommand.Execute(null);
    }
}
