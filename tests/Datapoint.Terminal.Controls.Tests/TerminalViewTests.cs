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

    [AvaloniaFact]
    public void Marks_who_typed_each_command_and_links_the_agents()
    {
        var terminal = new EmulatorTerminal(80, 24);
        terminal.MarkNextCommandSource(Emulator.ShellIntegration.CommandSource.Agent);
        terminal.Feed("\e]133;A\a$ \e]133;B\auptime\r\n\e]133;C\a up 3 days\r\n\e]133;D;0\a");
        terminal.Feed("\e]133;A\a$ \e]133;B\als\r\n\e]133;C\afile\r\n\e]133;D;0\a\e]133;A\a$ ");
        var view = new TerminalView { Terminal = terminal, FontSize = 14, Padding = new Avalonia.Thickness(12, 10) };
        var clicked = new List<string?>();
        view.CommandMarkClicked += (_, e) => clicked.Add(e.Command.CommandLine);
        var window = new Window { Width = 640, Height = 360, Content = view };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var row = view.CellMetrics.Height;
        window.MouseDown(new Avalonia.Point(6, 10 + (row / 2)), MouseButton.Left);
        window.MouseUp(new Avalonia.Point(6, 10 + (row / 2)), MouseButton.Left);
        window.MouseDown(new Avalonia.Point(6, 10 + (row * 2.5)), MouseButton.Left);
        window.MouseUp(new Avalonia.Point(6, 10 + (row * 2.5)), MouseButton.Left);
        using var frame = window.CaptureRenderedFrame();
        Directory.CreateDirectory("rendered");
        frame?.Save("rendered/terminal-marks.png", new Avalonia.Media.Imaging.PngBitmapEncoderOptions());

        Assert.Equal(["uptime"], clicked);
    }
}
