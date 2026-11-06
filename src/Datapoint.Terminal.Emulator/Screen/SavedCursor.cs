namespace Datapoint.Terminal.Emulator.Screen;

public sealed record SavedCursor(
    int Row,
    int Column,
    CellStyle Style,
    bool PendingWrap,
    Charset G0,
    Charset G1,
    bool UsesG1,
    bool OriginMode);
