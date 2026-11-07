using System.Text;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Datapoint.Terminal.Controls.Input;
using Datapoint.Terminal.Controls.Rendering;
using Datapoint.Terminal.Emulator.Screen;
using Datapoint.Terminal.Emulator.ShellIntegration;
using EmulatorTerminal = Datapoint.Terminal.Emulator.Terminal;

namespace Datapoint.Terminal.Controls;

public sealed partial class TerminalView : Control
{
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        AvaloniaProperty.Register<TerminalView, FontFamily>(nameof(FontFamily), new FontFamily("Cascadia Mono, SF Mono, Menlo, Consolas, DejaVu Sans Mono, Liberation Mono, monospace"));

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<TerminalView, double>(nameof(FontSize), 14);

    // Product decision 80: a mark beside each command's line in the left padding, solid for the agent's, a thin line for the
    // user's, so the difference doesn't rest on colour alone.
    public static readonly StyledProperty<IBrush?> AgentMarkBrushProperty =
        AvaloniaProperty.Register<TerminalView, IBrush?>(nameof(AgentMarkBrush), Brushes.DodgerBlue);

    public static readonly StyledProperty<IBrush?> UserMarkBrushProperty =
        AvaloniaProperty.Register<TerminalView, IBrush?>(nameof(UserMarkBrush), Brushes.Gray);

    public static readonly StyledProperty<Thickness> PaddingProperty =
        AvaloniaProperty.Register<TerminalView, Thickness>(nameof(Padding));

    private const int WheelLines = 3;

    private readonly RowRenderer _renderer = new();
    private EmulatorTerminal? _terminal;
    private int _frameRequested;
    private int _scrollOffset;

    static TerminalView()
    {
        FocusableProperty.OverrideDefaultValue<TerminalView>(true);
        AffectsRender<TerminalView>(FontFamilyProperty, FontSizeProperty, PaddingProperty);
    }

    public TerminalView()
    {
        // Tunnel, so Tab and the arrow keys reach the shell instead of moving focus.
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
        ClipToBounds = true;
    }

    public event EventHandler<TerminalInputEventArgs>? Input;

    public event EventHandler<TerminalResizedEventArgs>? Resized;

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public IBrush? AgentMarkBrush
    {
        get => GetValue(AgentMarkBrushProperty);
        set => SetValue(AgentMarkBrushProperty, value);
    }

    public IBrush? UserMarkBrush
    {
        get => GetValue(UserMarkBrushProperty);
        set => SetValue(UserMarkBrushProperty, value);
    }

    // Raised when an agent's mark is clicked, to show the chat step that sent the command.
    public event EventHandler<CommandMarkEventArgs>? CommandMarkClicked;

    public Thickness Padding
    {
        get => GetValue(PaddingProperty);
        set => SetValue(PaddingProperty, value);
    }

    // Off on macOS, where Option types characters.
    public bool AltSendsEscape { get; set; } = !OperatingSystem.IsMacOS();

    public EmulatorTerminal? Terminal
    {
        get => _terminal;
        set
        {
            _terminal = value;
            _scrollOffset = 0;
            UpdateGridSize(Bounds.Size);
            InvalidateVisual();
        }
    }

    public CellMetrics CellMetrics => _renderer.Metrics;

    public void NotifyOutput()
    {
        if (Interlocked.Exchange(ref _frameRequested, 1) == 1)
        {
            return;
        }

        Dispatcher.UIThread.Post(RequestFrame, DispatcherPriority.Render);
    }

    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var terminal = _terminal;
        var palette = terminal?.Palette ?? new ColorPalette();
        context.DrawRectangle(_renderer.Brush(palette.Background), null, new Rect(Bounds.Size));
        if (terminal is null)
        {
            return;
        }

        using var padding = context.PushTransform(Matrix.CreateTranslation(Padding.Left, Padding.Top));
        lock (terminal.SyncRoot)
        {
            var metrics = _renderer.Metrics;
            for (var index = 0; index < terminal.Rows; index++)
            {
                _renderer.Draw(context, VisibleRow(terminal, index), index * metrics.Height, palette);
            }

            DrawSelection(context, terminal);
            DrawSelectionCaret(context, terminal);
            DrawCursor(context, terminal, palette);
            DrawMarks(context, terminal);
        }
    }

    private const double MarkWidth = 3;

    private void DrawMarks(DrawingContext context, EmulatorTerminal terminal)
    {
        if (terminal.IsAlternateScreen || Padding.Left < MarkWidth + 2)
        {
            return;
        }

        var height = _renderer.Metrics.Height;
        var x = -Padding.Left + ((Padding.Left - MarkWidth) / 2);
        foreach (var (command, row) in VisibleCommands(terminal))
        {
            var isAgent = command.Source == CommandSource.Agent;
            var brush = isAgent ? AgentMarkBrush : UserMarkBrush;
            var width = isAgent ? MarkWidth : 1;
            context.DrawRectangle(brush, null, new Rect(x + ((MarkWidth - width) / 2), (row * height) + 2, width, height - 4), 1, 1);
        }
    }

    // Each command whose typed line is on screen, with its row.
    private IEnumerable<(CommandRecord Command, int Row)> VisibleCommands(EmulatorTerminal terminal)
    {
        var top = terminal.Primary.FirstVisibleLine - _scrollOffset;
        foreach (var command in terminal.Commands)
        {
            var line = command.InputLine ?? command.PromptLine;
            var row = line - top;
            if (command.CommandLine is not null && row >= 0 && row < terminal.Rows)
            {
                yield return (command, (int)row);
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == PaddingProperty)
        {
            UpdateGridSize(Bounds.Size);
            return;
        }

        if (change.Property != FontFamilyProperty && change.Property != FontSizeProperty)
        {
            return;
        }

        ConfigureFont();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        ConfigureFont();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var metrics = _renderer.Metrics;
        var columns = _terminal?.Columns ?? 80;
        var rows = _terminal?.Rows ?? 24;
        var width = double.IsInfinity(availableSize.Width) ? (columns * metrics.Width) + Padding.Left + Padding.Right : availableSize.Width;
        var height = double.IsInfinity(availableSize.Height) ? (rows * metrics.Height) + Padding.Top + Padding.Bottom : availableSize.Height;
        return new Size(width, height);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnSizeChanged(e);
        UpdateGridSize(e.NewSize);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnTextInput(e);
        var text = e.Text;
        if (string.IsNullOrEmpty(text) || text.All(char.IsControl))
        {
            // Keys that produce control characters were already sent from KeyDown.
            return;
        }

        e.Handled = true;
        Send(Encoding.UTF8.GetBytes(text));
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerWheelChanged(e);
        var terminal = _terminal;
        if (terminal is null)
        {
            return;
        }

        var lines = (int)Math.Round(e.Delta.Y * WheelLines);
        e.Handled = true;
        if (terminal.IsAlternateScreen)
        {
            // Full-screen programs without mouse reporting expect arrow keys, as other terminals send.
            var key = lines > 0 ? Key.Up : Key.Down;
            for (var count = 0; count < Math.Abs(lines); count++)
            {
                Send(KeyEncoder.Encode(key, KeyModifiers.None, terminal.Modes, AltSendsEscape)!);
            }

            return;
        }

        lock (terminal.SyncRoot)
        {
            _scrollOffset = Math.Clamp(_scrollOffset + lines, 0, terminal.Primary.Scrollback.Count);
        }

        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        if (MarkAt(e.GetPosition(this)) is { } command)
        {
            e.Handled = true;
            CommandMarkClicked?.Invoke(this, new CommandMarkEventArgs(command));
            return;
        }

        e.Handled = StartSelection(e);
    }

    private CommandRecord? MarkAt(Point point)
    {
        var terminal = _terminal;
        if (terminal is null || point.X >= Padding.Left)
        {
            return null;
        }

        var row = (int)Math.Floor((point.Y - Padding.Top) / _renderer.Metrics.Height);
        lock (terminal.SyncRoot)
        {
            return VisibleCommands(terminal).FirstOrDefault(item => item.Row == row && item.Command.Source == CommandSource.Agent).Command;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new TerminalViewAutomationPeer(this);

    private static Row VisibleRow(EmulatorTerminal terminal, int index, int scrollOffset)
    {
        if (scrollOffset == 0 || terminal.IsAlternateScreen)
        {
            return terminal.Screen[index];
        }

        var scrollback = terminal.Primary.Scrollback;
        var line = scrollback.Count - scrollOffset + index;
        return line < scrollback.Count ? scrollback[line] : terminal.Primary[line - scrollback.Count];
    }

    private Row VisibleRow(EmulatorTerminal terminal, int index) => VisibleRow(terminal, index, _scrollOffset);

    private void RequestFrame()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
        {
            Interlocked.Exchange(ref _frameRequested, 0);
            InvalidateVisual();
            return;
        }

        topLevel.RequestAnimationFrame(_ =>
        {
            Interlocked.Exchange(ref _frameRequested, 0);
            InvalidateVisual();
        });
    }

    private void ConfigureFont()
    {
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        _renderer.Configure(FontFamily, FontSize, scaling);
        UpdateGridSize(Bounds.Size);
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void UpdateGridSize(Size size)
    {
        var terminal = _terminal;
        var metrics = _renderer.Metrics;
        if (terminal is null || size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        size = size.Deflate(Padding);
        var columns = Math.Max(2, (int)(size.Width / metrics.Width));
        var rows = Math.Max(1, (int)(size.Height / metrics.Height));
        lock (terminal.SyncRoot)
        {
            if (columns == terminal.Columns && rows == terminal.Rows)
            {
                return;
            }

            // A new width reflows the text, and a selection kept by line and column would cover different text.
            if (columns != terminal.Columns)
            {
                ClearSelection();
            }

            terminal.Resize(columns, rows);
            _scrollOffset = 0;
        }

        Resized?.Invoke(this, new TerminalResizedEventArgs(columns, rows));
        InvalidateVisual();
    }

    private void DrawCursor(DrawingContext context, EmulatorTerminal terminal, ColorPalette palette)
    {
        if (!terminal.Modes.CursorVisible || _scrollOffset != 0)
        {
            return;
        }

        var metrics = _renderer.Metrics;
        var column = Math.Min(terminal.Cursor.Column, terminal.Columns - 1);
        var cell = terminal.Screen[terminal.Cursor.Row][column];
        var x = column * metrics.Width;
        var y = terminal.Cursor.Row * metrics.Height;
        var width = Math.Max(1, (int)cell.Width) * metrics.Width;
        var brush = _renderer.Brush(palette.CursorColor);
        switch (terminal.Modes.CursorShape)
        {
            case CursorShape.BlinkingBar or CursorShape.SteadyBar:
                context.DrawRectangle(brush, null, new Rect(x, y, Math.Max(1, metrics.Width / 8), metrics.Height));
                return;
            case CursorShape.BlinkingUnderline or CursorShape.SteadyUnderline:
                context.DrawRectangle(brush, null, new Rect(x, y + metrics.Height - 2, width, 2));
                return;
            default:
                DrawBlockCursor(context, cell, new Rect(x, y, width, metrics.Height), brush, palette);
                return;
        }
    }

    private void DrawBlockCursor(DrawingContext context, Cell cell, Rect rect, IBrush brush, ColorPalette palette)
    {
        if (!IsFocused)
        {
            context.DrawRectangle(null, new Pen(brush), rect.Deflate(0.5));
            return;
        }

        context.DrawRectangle(brush, null, rect);
        if (cell.CodePoint == 0)
        {
            return;
        }

        var (_, background) = RowRenderer.Colors(cell.Style, palette);
        context.DrawText(_renderer.Text(cell.Text, cell.Style, _renderer.Brush(background)), rect.TopLeft);
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        var terminal = _terminal;
        if (terminal is null || e.Handled)
        {
            return;
        }

        if (IsSelectingWithKeyboard)
        {
            // Shortcuts with ⌘ or Ctrl still reach the menus; nothing goes to the shell.
            if (!IsCopyGesture(e) && (e.KeyModifiers & (KeyModifiers.Meta | KeyModifiers.Control)) != KeyModifiers.None)
            {
                return;
            }

            HandleSelectionKey(terminal, e);
            return;
        }

        if (IsCopyGesture(e))
        {
            // Without a selection, ⌘C has nothing to copy; on other systems Ctrl+Shift+C isn't a key a shell expects either.
            e.Handled = true;
            _ = CopyAsync();
            return;
        }

        if (IsPasteGesture(e))
        {
            e.Handled = true;
            _ = PasteAsync();
            return;
        }

        var bytes = KeyEncoder.Encode(e.Key, e.KeyModifiers, terminal.Modes, AltSendsEscape);
        if (bytes is null)
        {
            return;
        }

        e.Handled = true;
        Send(bytes);
    }

    private static bool IsPasteGesture(KeyEventArgs e) => OperatingSystem.IsMacOS()
        ? e.Key == Key.V && e.KeyModifiers == KeyModifiers.Meta
        : e.Key == Key.V && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift);

    private async Task PasteAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null || _terminal is null)
        {
            return;
        }

        var text = await clipboard.TryGetTextAsync().ConfigureAwait(true);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Send(PasteEncoder.Encode(text, _terminal.Modes.BracketedPaste));
    }

    private void Send(byte[] data)
    {
        if (_scrollOffset != 0)
        {
            _scrollOffset = 0;
            InvalidateVisual();
        }

        Input?.Invoke(this, new TerminalInputEventArgs(data));
    }

    internal string GetVisibleText() => _terminal is null ? string.Empty : _terminal.GetVisibleText();
}
