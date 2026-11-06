using System.Globalization;
using System.Text;
using Datapoint.Terminal.Emulator.Parsing;

namespace Datapoint.Terminal.Emulator;

public sealed partial class Terminal
{
    private const int MaxDcsRequest = 64;

    private readonly StringBuilder _dcsData = new();
    private DcsRequest _dcsRequest;

    private enum DcsRequest
    {
        None,
        StatusString,
        TerminalCapability,
    }

    public void DcsHook(CsiSequence sequence)
    {
        _dcsData.Clear();
        _dcsRequest = (sequence.Intermediates.Length == 1 ? (char)sequence.Intermediates[0] : '\0', (char)sequence.Final) switch
        {
            ('$', 'q') => DcsRequest.StatusString,
            ('+', 'q') => DcsRequest.TerminalCapability,
            _ => DcsRequest.None,
        };
    }

    public void DcsPut(byte data)
    {
        if (_dcsRequest == DcsRequest.None || _dcsData.Length >= MaxDcsRequest)
        {
            return;
        }

        _dcsData.Append((char)data);
    }

    public void DcsUnhook()
    {
        var request = _dcsRequest;
        _dcsRequest = DcsRequest.None;
        switch (request)
        {
            case DcsRequest.StatusString:
                ReplyStatusString(_dcsData.ToString());
                break;
            case DcsRequest.TerminalCapability:
                Reply("\eP0+r\e\\");
                break;
        }
    }

    public void ApcDispatch(ReadOnlySpan<byte> data)
    {
    }

    private void ReplyStatusString(string request)
    {
        var answer = request switch
        {
            "r" => string.Create(CultureInfo.InvariantCulture, $"{ScrollTop + 1};{ScrollBottom + 1}r"),
            " q" => string.Create(CultureInfo.InvariantCulture, $"{(int)Modes.CursorShape} q"),
            "m" => "0m",
            _ => null,
        };
        Reply(answer is null ? "\eP0$r\e\\" : $"\eP1$r{answer}\e\\");
    }
}
