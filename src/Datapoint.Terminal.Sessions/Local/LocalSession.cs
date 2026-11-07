using Datapoint.Terminal.Security.Cryptography;
using Datapoint.Terminal.Sessions.ShellIntegration;
using Porta.Pty;

namespace Datapoint.Terminal.Sessions.Local;

public sealed class LocalSession : ITerminalSession
{
    private static readonly string[] s_localeVariables = ["LANG", "LC_ALL", "LC_CTYPE"];

    private readonly IPtyConnection _pty;
    private readonly int _master;
    private readonly ShellIntegrationInstaller? _integration;
    private readonly TaskCompletionSource<int?> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private LocalSession(IPtyConnection pty, ShellIntegrationInstaller? integration, string? nonce, ShellKind kind)
    {
        _pty = pty;
        _master = MasterDescriptor(pty);
        _integration = integration;
        ShellIntegrationNonce = nonce;
        Kind = kind;
        _pty.ProcessExited += (_, e) => _exited.TrySetResult(e.ExitCode);
    }

    public string? ShellIntegrationNonce { get; }

    public ShellKind Kind { get; }

    public bool? IsInputHidden => TerminalModes.IsInputHidden(_master);

    public int ProcessId => _pty.Pid;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD003", Justification = "Completed by the pseudo-terminal's exit event, never on the caller's context.")]
    public Task<int?> Exited => _exited.Task;

    public static async Task<LocalSession> StartAsync(LocalSessionOptions options, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        var program = options.Shell ?? DefaultShell.Find();
        var integration = options.ShellIntegration && options.Arguments is null ? ShellIntegrationInstaller.Create() : null;
        try
        {
            var nonce = integration is null ? null : Convert.ToHexStringLower(RandomBytes.Create(16));
            var launch = integration?.Prepare(program, options.Login, nonce!)
                ?? PlainLaunch(program, options);
            var pty = await PtyProvider.SpawnAsync(
                new PtyOptions
                {
                    Name = "Datapoint Terminal",
                    App = launch.Program,
                    CommandLine = [.. launch.Arguments],
                    Cwd = options.WorkingDirectory,
                    Cols = options.Columns,
                    Rows = options.Rows,
                    Environment = BuildEnvironment(launch, options),
                },
                cancellationToken).ConfigureAwait(false);
            return new LocalSession(pty, integration, nonce, launch.Kind);
        }
        catch
        {
            integration?.Dispose();
            throw;
        }
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            return await _pty.ReaderStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // macOS and Linux report the end of the shell as an I/O error on the pseudo-terminal.
            return 0;
        }
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        await _pty.WriterStream.WriteAsync(data, cancellationToken).ConfigureAwait(false);
        await _pty.WriterStream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task ResizeAsync(int columns, int rows, CancellationToken cancellationToken)
    {
        _pty.Resize(columns, rows);
        return Task.CompletedTask;
    }

    // Porta.Pty keeps the pseudo-terminal's descriptor inside its streams (a field "fd", an int or a descriptor holding one); -1
    // when a version keeps it elsewhere.
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "If trimming removes the field, the descriptor isn't found and hidden input is just not detected.")]
    private static int MasterDescriptor(IPtyConnection pty)
    {
        object? holder = pty.ReaderStream;
        for (var depth = 0; depth < 3 && holder is not null; depth++)
        {
            var value = FieldNamed(holder, "fd") ?? FieldNamed(holder, "MasterFd") ?? FieldNamed(holder, "descriptor") ?? FieldNamed(holder, "_descriptor");
            if (value is int descriptor)
            {
                return descriptor;
            }

            holder = value;
        }

        return -1;
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "As above.")]
    private static object? FieldNamed(object holder, string name)
    {
        const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        for (var type = holder.GetType(); type is not null; type = type.BaseType)
        {
            if (type.GetField(name, Any) is { } field)
            {
                return field.GetValue(holder);
            }
        }

        return null;
    }

    public ValueTask DisposeAsync()
    {
        if (!_exited.Task.IsCompleted)
        {
            _pty.Kill();
        }

        _pty.Dispose();
        _integration?.Dispose();
        return ValueTask.CompletedTask;
    }

    private static ShellLaunch PlainLaunch(string program, LocalSessionOptions options) =>
        options.Arguments is { } arguments
            ? new ShellLaunch(program, arguments, new Dictionary<string, string>(StringComparer.Ordinal), ShellKind.Other)
            : new ShellLaunch(program, options.Login && !OperatingSystem.IsWindows() ? ["-l"] : [], new Dictionary<string, string>(StringComparer.Ordinal), ShellIntegrationInstaller.KindOf(program));

    private static Dictionary<string, string> BuildEnvironment(ShellLaunch launch, LocalSessionOptions options)
    {
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["TERM"] = "xterm-256color",
            ["COLORTERM"] = "truecolor",
            ["TERM_PROGRAM"] = "DatapointTerminal",
        };
        var hasLocale = s_localeVariables.Any(name => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)));
        if (!hasLocale && !OperatingSystem.IsWindows())
        {
            // Apps started from the macOS Dock get no locale, which leaves shells in ASCII.
            environment["LANG"] = OperatingSystem.IsMacOS() ? "en_US.UTF-8" : "C.UTF-8";
        }

        foreach (var (name, value) in launch.Environment)
        {
            environment[name] = value;
        }

        foreach (var (name, value) in options.Environment)
        {
            environment[name] = value;
        }

        return environment;
    }
}
