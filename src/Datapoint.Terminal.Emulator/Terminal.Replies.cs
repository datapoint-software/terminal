using System.Globalization;
using System.Reflection;
using System.Text;

namespace Datapoint.Terminal.Emulator;

public sealed partial class Terminal
{
    public const string Name = "DatapointTerminal";

    private static readonly string s_version =
        typeof(Terminal).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    private void Reply(string text) => _host.Reply(Encoding.ASCII.GetBytes(text));

    private void ReplyPrimaryDeviceAttributes(int parameter)
    {
        if (parameter != 0)
        {
            return;
        }

        Reply("\e[?62;22c");
    }

    private void ReplySecondaryDeviceAttributes(int parameter)
    {
        if (parameter != 0)
        {
            return;
        }

        Reply("\e[>1;10;0c");
    }

    private void ReplyVersion() => Reply($"\eP>|{Name}({s_version})\e\\");

    private void ReplyDeviceStatus(int request)
    {
        if (request == 5)
        {
            Reply("\e[0n");
            return;
        }

        if (request != 6)
        {
            return;
        }

        var row = Cursor.Row - (Modes.Origin ? ScrollTop : 0) + 1;
        Reply(string.Create(CultureInfo.InvariantCulture, $"\e[{row};{Cursor.Column + 1}R"));
    }

    private void ReplyPrivateModeState(int mode) =>
        Reply(string.Create(CultureInfo.InvariantCulture, $"\e[?{mode};{PrivateModeState(mode)}$y"));

    private void ReplyAnsiModeState(int mode)
    {
        var state = mode switch
        {
            4 => StateOf(Modes.Insert),
            20 => StateOf(Modes.LineFeedNewLine),
            _ => 0,
        };
        Reply(string.Create(CultureInfo.InvariantCulture, $"\e[{mode};{state}$y"));
    }
}
