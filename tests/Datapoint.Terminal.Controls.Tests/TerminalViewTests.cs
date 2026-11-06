using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using EmulatorTerminal = Datapoint.Terminal.Emulator.Terminal;

namespace Datapoint.Terminal.Controls.Tests;

public sealed class TerminalViewTests
{
    private static (Window Window, TerminalView View, List<string> Input) Show(EmulatorTerminal terminal)
    {
        var view = new TerminalView { Terminal = terminal, FontSize = 14 };
        var input = new List<string>();
        view.Input += (_, e) => input.Add(Encoding.UTF8.GetString(e.Data));
        var window = new Window { Width = 640, Height = 360, Content = view };
        window.Show();
        view.Focus();
        return (window, view, input);
    }

    [AvaloniaFact]
    public void Fits_the_grid_to_the_window()
    {
        var terminal = new EmulatorTerminal(80, 24);
        var (_, view, _) = Show(terminal);

        Assert.Equal((int)(view.Bounds.Width / view.CellMetrics.Width), terminal.Columns);
        Assert.Equal((int)(view.Bounds.Height / view.CellMetrics.Height), terminal.Rows);
    }

    [AvaloniaFact]
    public void Sends_typed_text_and_keys()
    {
        var (window, _, input) = Show(new EmulatorTerminal(80, 24));

        window.KeyTextInput("ls");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.Control);

        Assert.Equal(["ls", "\r", "\x03"], input);
    }

    [AvaloniaFact]
    public void Keeps_tab_in_the_terminal()
    {
        var (window, view, input) = Show(new EmulatorTerminal(80, 24));

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);

        Assert.Equal(["\t"], input);
        Assert.True(view.IsFocused);
    }

    [AvaloniaFact]
    public void Renders_colored_text()
    {
        var terminal = new EmulatorTerminal(80, 24);
        terminal.Feed("\e[1;32mDatapoint\e[0m Terminal \e[7m inverse \e[0m 漢字 ✓\r\n$ ");
        var (window, _, _) = Show(terminal);

        var frame = window.CaptureRenderedFrame();

        Assert.NotNull(frame);
        var folder = Path.Combine(AppContext.BaseDirectory, "rendered");
        Directory.CreateDirectory(folder);
        frame!.Save(Path.Combine(folder, "colored-text.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
    }
}
