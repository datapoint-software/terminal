using System.Diagnostics;
using Datapoint.Terminal.Agent.Projects;

namespace Datapoint.Terminal.Agent.Tests.Projects;

public sealed class ProjectContainerTests : IDisposable
{
    private readonly string _bin = Directory.CreateTempSubdirectory("datapoint-bin-").FullName;

    public void Dispose() => Directory.Delete(_bin, recursive: true);

    [Fact]
    public void Opens_a_docker_container_with_the_shell_it_names() =>
        Assert.Equal(["docker", "exec", "-it", "web", "/bin/bash"], new ProjectContainer { Name = "web", Shell = "/bin/bash" }.Command(), StringComparer.Ordinal);

    [Fact]
    public void Opens_a_pod_in_its_namespace()
    {
        var pod = new ProjectContainer { Kind = ContainerKind.Kubernetes, Name = "api-0", Namespace = "shop", Shell = "sh" };

        Assert.Equal(["kubectl", "exec", "-it", "-n", "shop", "api-0", "--", "sh"], pod.Command(), StringComparer.Ordinal);
        Assert.Equal("kubernetes:shop/api-0", pod.Address());
    }

    [Fact]
    public void Prefers_bash_when_no_shell_is_named()
    {
        var command = new ProjectContainer { Name = "web" }.Command();

        Assert.Equal(["sh", "-c"], command.Skip(4).Take(2), StringComparer.Ordinal);
        Assert.Contains("exec bash", command[^1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("-it")]
    [InlineData("web;reboot")]
    [InlineData("web name")]
    [InlineData("")]
    public void Refuses_a_name_that_isnt_a_container_name(string name) =>
        Assert.NotNull(new ProjectContainer { Name = name }.Problem("app"));

    [Theory]
    [InlineData("bash -c reboot")]
    [InlineData("-c")]
    [InlineData("$(reboot)")]
    [InlineData("../../bin/sh")]
    public void Refuses_a_shell_that_isnt_a_program(string shell) =>
        Assert.NotNull(new ProjectContainer { Name = "web", Shell = shell }.Problem("app"));

    [Fact]
    public void Refuses_a_namespace_for_docker() =>
        Assert.NotNull(new ProjectContainer { Name = "web", Namespace = "shop" }.Problem("app"));

    [Fact]
    public void Refuses_forwards_without_an_ssh_host() =>
        Assert.NotNull(new ProjectConnection { Name = "db", Container = new ProjectContainer { Name = "postgres" }, Forwards = ["5432 localhost:5432"] }.Problem());

    [Fact]
    public void Refuses_a_forward_it_cant_read() =>
        Assert.NotNull(new ProjectConnection { Name = "web", SshHost = "web", Forwards = ["5432"] }.Problem());

    [Fact]
    public void A_container_alone_is_a_local_connection()
    {
        var connection = new ProjectConnection { Name = "db", Container = new ProjectContainer { Name = "postgres" } };

        Assert.True(connection.IsLocal);
        Assert.Null(connection.Problem());
    }

    // The command line goes to a real shell, as it does locally and on an SSH host, with a stand-in docker that lists what it got.
    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task The_command_line_reaches_docker_as_separate_arguments()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "POSIX shells only.");
        var docker = Path.Combine(_bin, "docker");
        await File.WriteAllTextAsync(docker, "#!/bin/sh\nfor argument in \"$@\"; do printf '%s\\n' \"$argument\"; done\n", TestContext.Current.CancellationToken);
        File.SetUnixFileMode(docker, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var container = new ProjectContainer { Name = "web" };
        var start = new ProcessStartInfo("/bin/sh") { RedirectStandardOutput = true, UseShellExecute = false };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(container.CommandLine());
        start.Environment["PATH"] = _bin + ":/usr/bin:/bin";

        using var process = Process.Start(start)!;
        var output = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        await process.WaitForExitAsync(TestContext.Current.CancellationToken);

        Assert.Equal(container.Command().Skip(1), output.TrimEnd('\n').Split('\n'), StringComparer.Ordinal);
    }
}
