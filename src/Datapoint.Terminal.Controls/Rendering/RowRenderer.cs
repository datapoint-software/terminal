using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Datapoint.Terminal.Emulator.Screen;

namespace Datapoint.Terminal.Controls.Rendering;

internal sealed class RowRenderer
{
    private readonly ConditionalWeakTable<Row, RowDrawing> _cache = [];
    private readonly Dictionary<int, IImmutableSolidColorBrush> _brushes = [];
    private Typeface[] _typefaces = [];
    private double _fontSize;
    private int _generation;

    public CellMetrics Metrics { get; private set; } = new(8, 16, 12);

    public void Configure(FontFamily family, double fontSize, double renderScaling)
    {
        _typefaces =
        [
            new Typeface(family),
            new Typeface(family, weight: FontWeight.Bold),
            new Typeface(family, FontStyle.Italic),
            new Typeface(family, FontStyle.Italic, FontWeight.Bold),
        ];
        _fontSize = fontSize;
        Metrics = CellMetrics.Measure(_typefaces[0], fontSize, renderScaling);
        Invalidate();
    }

    public void Invalidate() => _generation++;

    public IImmutableSolidColorBrush Brush(int rgb, double opacity = 1)
    {
        var key = rgb | ((int)(opacity * 100) << 24);
        if (!_brushes.TryGetValue(key, out var brush))
        {
            brush = new ImmutableSolidColorBrush(Color.FromUInt32(0xFF000000 | (uint)rgb), opacity);
            _brushes[key] = brush;
        }

        return brush;
    }

    public void Draw(DrawingContext context, Row row, double y, ColorPalette palette)
    {
        if (!_cache.TryGetValue(row, out var drawing) || drawing.ChangedAt != row.ChangedAt || drawing.Generation != _generation)
        {
            drawing = Build(row, palette);
            _cache.AddOrUpdate(row, drawing);
        }

        using (context.PushTransform(Matrix.CreateTranslation(0, y)))
        {
            foreach (var (rect, brush) in drawing.Backgrounds)
            {
                context.DrawRectangle(brush, null, rect);
            }

            foreach (var (text, x) in drawing.Texts)
            {
                context.DrawText(text, new Point(x, 0));
            }

            foreach (var (pen, from, to) in drawing.Lines)
            {
                context.DrawLine(pen, from, to);
            }
        }
    }

    public FormattedText Text(string text, CellStyle style, IBrush foreground) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, TypefaceFor(style), _fontSize, foreground);

    public static (int Foreground, int Background) Colors(CellStyle style, ColorPalette palette)
    {
        var foreground = palette.Resolve(style.Foreground, isForeground: true);
        var background = palette.Resolve(style.Background, isForeground: false);
        if (style.Has(CellAttributes.Inverse))
        {
            (foreground, background) = (background, foreground);
        }

        return (style.Has(CellAttributes.Invisible) ? background : foreground, background);
    }

    private Typeface TypefaceFor(CellStyle style)
    {
        var index = (style.Has(CellAttributes.Bold) ? 1 : 0) + (style.Has(CellAttributes.Italic) ? 2 : 0);
        return _typefaces[index];
    }

    private RowDrawing Build(Row row, ColorPalette palette)
    {
        var drawing = new RowDrawing(row.ChangedAt, _generation);
        var run = new TextRun();
        for (var column = 0; column < row.Columns; column++)
        {
            var cell = row[column];
            if (cell.IsWideContinuation)
            {
                continue;
            }

            AddDecorations(drawing, cell, column, palette);
            var isPlain = cell.Width == 1 && cell.Grapheme is null && cell.CodePoint is >= 0x20 and < 0x7F;
            if (!run.CanExtend(isPlain, cell.Style))
            {
                Flush(drawing, run, palette);
            }

            if (isPlain)
            {
                run.Add(column, (char)cell.CodePoint, cell.Style);
            }
            else if (cell.CodePoint != 0)
            {
                AddText(drawing, cell.Text, column, cell.Style, palette);
            }
        }

        Flush(drawing, run, palette);
        return drawing;
    }

    private void Flush(RowDrawing drawing, TextRun run, ColorPalette palette)
    {
        if (run.IsEmpty)
        {
            return;
        }

        AddText(drawing, run.Text, run.Start, run.Style, palette);
        run.Clear();
    }

    private void AddDecorations(RowDrawing drawing, Cell cell, int column, ColorPalette palette)
    {
        var (foreground, background) = Colors(cell.Style, palette);
        var x = column * Metrics.Width;
        var width = Math.Max(1, (int)cell.Width) * Metrics.Width;
        if (background != palette.Background)
        {
            drawing.Backgrounds.Add((new Rect(x, 0, width + 0.5, Metrics.Height), Brush(background)));
        }

        var pen = new ImmutablePen(Brush(foreground), Math.Max(1, _fontSize / 14));
        if (cell.Style.Underline != UnderlineStyle.None)
        {
            var y = Metrics.Baseline + 2;
            drawing.Lines.Add((pen, new Point(x, y), new Point(x + width, y)));
        }

        if (cell.Style.Has(CellAttributes.Strikethrough))
        {
            var y = Metrics.Height / 2;
            drawing.Lines.Add((pen, new Point(x, y), new Point(x + width, y)));
        }
    }

    private void AddText(RowDrawing drawing, string text, int column, CellStyle style, ColorPalette palette)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var (foreground, _) = Colors(style, palette);
        var opacity = style.Has(CellAttributes.Faint) ? 0.6 : 1;
        drawing.Texts.Add((Text(text, style, Brush(foreground, opacity)), column * Metrics.Width));
    }

    private sealed class TextRun
    {
        private readonly StringBuilder _text = new();

        public int Start { get; private set; }

        public CellStyle Style { get; private set; }

        public bool IsEmpty => _text.Length == 0;

        public string Text => _text.ToString();

        public bool CanExtend(bool isPlain, CellStyle style) => IsEmpty || (isPlain && style == Style);

        public void Add(int column, char character, CellStyle style)
        {
            if (IsEmpty)
            {
                Start = column;
                Style = style;
            }

            _text.Append(character);
        }

        public void Clear() => _text.Clear();
    }

    private sealed class RowDrawing(long changedAt, int generation)
    {
        public long ChangedAt { get; } = changedAt;

        public int Generation { get; } = generation;

        public List<(Rect Rect, IBrush Brush)> Backgrounds { get; } = [];

        public List<(FormattedText Text, double X)> Texts { get; } = [];

        public List<(IPen Pen, Point From, Point To)> Lines { get; } = [];
    }
}
