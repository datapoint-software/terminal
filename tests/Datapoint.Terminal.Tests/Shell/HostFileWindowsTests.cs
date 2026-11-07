using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Datapoint.Terminal.Agent.Files;
using Datapoint.Terminal.Editor;
using Datapoint.Terminal.Shell;

namespace Datapoint.Terminal.Tests.Shell;

public sealed class HostFileWindowsTests
{
    private const string NginxConf = """
        # Managed by hand; see hosts/web1.md
        user www-data;
        worker_processes auto;

        events {
            worker_connections 1024;
        }

        http {
            include /etc/nginx/mime.types;
            sendfile on;
            keepalive_timeout 65;
            client_max_body_size 10m;

            log_format main '$remote_addr [$time_local] "$request" $status';
            access_log /var/log/nginx/access.log main;

            include /etc/nginx/sites-enabled/*;
        }

        """;

    private static ServerFileViewModel Nginx() => new("web1", "production", "/etc/nginx/nginx.conf", Encoding.UTF8.GetBytes(NginxConf), needsSudo: true);

    [AvaloniaFact]
    public void Shows_the_file_coloured_with_where_it_is()
    {
        var file = Nginx();
        var view = new ServerFileView { DataContext = file };
        var window = new Window { Width = 760, Height = 520, Content = view };
        window.Show();
        view.FindControl<AvaloniaEdit.TextEditor>("Editor")!.Document.Replace(NginxConf.IndexOf("10m", StringComparison.Ordinal), 3, "50m");
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(300);
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Save("rendered/host-file-editor.png", new PngBitmapEncoderOptions());

        Assert.True(file.IsDirty);
        Assert.Contains("client_max_body_size 50m;", file.Text, StringComparison.Ordinal);
        Assert.Equal("Edited · not saved", file.StatusLeft);
        Assert.StartsWith("nginx  ·  UTF-8  ·  LF", file.StatusRight, StringComparison.Ordinal);

        file.RevertCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(NginxConf, view.FindControl<AvaloniaEdit.TextEditor>("Editor")!.Text);
    }

    [AvaloniaFact]
    public void Lists_the_folder_and_goes_into_folders()
    {
        var files = new ListedFiles();
        var window = new OpenHostFileWindow(files, "web1", "production", "/etc/nginx/");
        window.Show();
        Pump();
        window.CaptureRenderedFrame()?.Save("rendered/host-file-open.png", new PngBitmapEncoderOptions());
        var list = window.FindControl<ListBox>("EntryList")!;
        var path = window.FindControl<TextBox>("PathBox")!;

        Assert.Equal(["conf.d/", "sites-enabled/", "mime.types", "nginx.conf"], list.Items.OfType<HostEntryItem>().Select(item => item.Display).ToList());

        path.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Pump();
        Assert.Equal("/etc/nginx/conf.d/", path.Text);
        Assert.Equal(["default.conf"], list.Items.OfType<HostEntryItem>().Select(item => item.Display).ToList());
    }

    [AvaloniaFact]
    public void Shows_the_change_and_the_command_before_saving()
    {
        var file = Nginx();
        var after = NginxConf.Replace("10m", "50m", StringComparison.Ordinal);
        var diff = TextDiff.Unified(file.Path, NginxConf, after);
        var command = FileEditCommand.Build(file.Path, HostFolders.Staging + "/…", "~/.datapoint-terminal/backups/etc/nginx/nginx.conf.20261107-143200", useSudo: true);
        var window = new SaveHostFileWindow();
        window.Fill(file, diff, command, "~/.datapoint-terminal/backups/etc/nginx/nginx.conf.20261107-143200");
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()?.Save("rendered/host-file-save.png", new PngBitmapEncoderOptions());

        Assert.Equal(1, SaveHostFileWindow.ChangedLines(diff));
        Assert.True(window.FindControl<Border>("ProductionNote")!.IsVisible);
        Assert.Contains("sudo tee", window.FindControl<SelectableTextBlock>("Command")!.Text, StringComparison.Ordinal);
    }

    private static void Pump()
    {
        for (var round = 0; round < 5; round++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(20);
        }
    }

    private sealed class ListedFiles : IHostFiles
    {
        public Task<IReadOnlyList<HostEntry>> ListAsync(string folder, int maxEntries, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<HostEntry>>(folder switch
            {
                "/etc/nginx/" => [new("nginx.conf", false), new("conf.d", true), new("mime.types", false), new("sites-enabled", true), new(".hidden", false)],
                "/etc/nginx/conf.d/" => [new("default.conf", false)],
                _ => throw new IOException("There's no such folder."),
            });

        public Task<HostFile> ReadAsync(string path, int maxBytes, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<string> StageAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
