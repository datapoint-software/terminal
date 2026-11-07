using System.Text;
using Datapoint.Terminal.Editor;
using Datapoint.Terminal.Markdown;

namespace Datapoint.Terminal.Tests.Editor;

public sealed class ServerFileViewModelTests
{
    private static ServerFileViewModel Open(string text, string path = "/etc/nginx/nginx.conf") =>
        new("web1", "production", path, Encoding.UTF8.GetBytes(text), needsSudo: true);

    [Fact]
    public void Refuses_what_saving_would_change()
    {
        Assert.Null(ServerFileViewModel.ProblemWith("user www-data;\n"u8.ToArray(), isTruncated: false));
        Assert.NotNull(ServerFileViewModel.ProblemWith([0x7f, 0x45, 0x4c, 0x46, 0x00], isTruncated: false));
        Assert.NotNull(ServerFileViewModel.ProblemWith([0x63, 0x61, 0x66, 0xe9], isTruncated: false));
        Assert.NotNull(ServerFileViewModel.ProblemWith("x"u8.ToArray(), isTruncated: true));
    }

    [Fact]
    public void Tracks_changes_and_reverts_them()
    {
        var file = Open("a\n");

        file.Text = "b\n";
        Assert.True(file.IsDirty);
        Assert.True(file.IsUnsaved);
        Assert.True(file.SaveCommand.CanExecute(null));

        file.RevertCommand.Execute(null);
        Assert.False(file.IsDirty);
        Assert.Equal("a\n", file.Text);
    }

    [Fact]
    public void Keeps_the_byte_order_mark_and_line_endings()
    {
        byte[] content = [.. Encoding.UTF8.Preamble, .. "a\r\nb\r\n"u8.ToArray()];
        var file = new ServerFileViewModel("box", null, "C:/x.ini", content, needsSudo: false);

        Assert.Equal("a\r\nb\r\n", file.Text);
        Assert.Equal("CRLF", file.LineEnding);
        Assert.Equal(content, file.Encode(file.Text));
    }

    [Fact]
    public void Is_saved_once_the_host_has_the_new_content()
    {
        var file = Open("a\n");
        file.Text = "b\n";
        file.IsSaving = true;

        file.MarkSaved("b\n", "b\n"u8.ToArray(), needsSudo: true);

        Assert.Equal((false, false, "b\n"), (file.IsDirty, file.IsSaving, file.Saved));
        Assert.Equal("nginx.conf — web1", file.Name);
        Assert.Equal("web1:/etc/nginx/nginx.conf", file.ToolTip);
    }

    [Theory]
    [InlineData("/etc/nginx/sites-available/shop", "nginx")]
    [InlineData("/etc/nginx/nginx.conf", "nginx")]
    [InlineData("/etc/systemd/system/app.service", "ini")]
    [InlineData("/etc/ssh/sshd_config", "sshconfig")]
    [InlineData("/srv/app/docker-compose.yml", "yaml")]
    [InlineData("/srv/app/Dockerfile", "dockerfile")]
    [InlineData("/home/deploy/.bashrc", "sh")]
    [InlineData("/etc/hosts", null)]
    public void Knows_common_configuration_files(string path, string? language) =>
        Assert.Equal(language, FileLanguage.Of(path)?.Name);

    [Fact]
    public void Colours_nginx_directives_across_the_whole_file()
    {
        var text = "# comment\nhttp {\n    include /etc/nginx/mime.types;\n    log_format main '$remote_addr';\n}\n";

        var spans = SyntaxColorizer.Spans(text, "nginx");
        var coloured = spans.Select(span => (text[span.Start..span.End], span.Kind)).ToList();

        Assert.Contains(("# comment", SyntaxKind.Comment), coloured);
        Assert.Contains(("http", SyntaxKind.Keyword), coloured);
        Assert.Contains(("    include", SyntaxKind.Keyword), coloured);
        Assert.Contains(("'$remote_addr'", SyntaxKind.String), coloured);
        Assert.Empty(SyntaxColorizer.Spans(text, null));
    }
}
