namespace Datapoint.Terminal.Emulator.Screen;

public sealed class Cursor
{
    public int Row { get; set; }

    public int Column { get; set; }

    public CellStyle Style { get; set; } = CellStyle.Default;

    public int HyperlinkId { get; set; }

    public bool PendingWrap { get; set; }

    public Charset G0 { get; set; }

    public Charset G1 { get; set; }

    public bool UsesG1 { get; set; }

    public Charset ActiveCharset => UsesG1 ? G1 : G0;

    public SavedCursor Save(bool originMode) =>
        new(Row, Column, Style, PendingWrap, G0, G1, UsesG1, originMode);

    public void Restore(SavedCursor saved)
    {
        Row = saved.Row;
        Column = saved.Column;
        Style = saved.Style;
        PendingWrap = saved.PendingWrap;
        G0 = saved.G0;
        G1 = saved.G1;
        UsesG1 = saved.UsesG1;
    }
}
