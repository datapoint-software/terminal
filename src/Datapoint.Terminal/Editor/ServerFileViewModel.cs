using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Datapoint.Terminal.Markdown;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Editor;

// A file on the machine a terminal runs on, open for editing. Nothing is saved until the user reviews the change; the window
// then saves it through that terminal, where the user sees the command.
public sealed partial class ServerFileViewModel : ObservableObject, IOpenDocument
{
    public const int MaxBytes = 1024 * 1024;

    private static readonly UTF8Encoding s_strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly bool _hasBom;

    public ServerFileViewModel(string host, string? environment, string path, byte[] content, bool needsSudo)
    {
        ArgumentNullException.ThrowIfNull(content);
        Host = host;
        Environment = environment;
        Path = path;
        NeedsSudo = needsSudo;
        _hasBom = content.AsSpan().StartsWith(Encoding.UTF8.Preamble);
        Content = content;
        Saved = Decode(content);
        Text = Saved;
        Language = FileLanguage.Of(path);
    }

    public string Host { get; }

    public string? Environment { get; }

    public bool HasEnvironment => Environment is not null;

    public bool IsProduction => Agent.EnvironmentLabels.IsProduction(Environment);

    public string Path { get; }

    public string FileName => Path[(Path.LastIndexOf('/') + 1)..];

    public string Name => Strings.Format(Strings.HostFileTabName, FileName, Host);

    public string Place => $"{Host}:{Path}";

    public string ToolTip => Place;

    public string AccessibleName => IsDirty ? Strings.Format(Strings.HostFileTabUnsaved, Name) : Name;

    public string CloseName => Strings.Format(Strings.DocumentClose, Name);

    public bool IsUnsaved => IsDirty;

    public bool NeedsSudo { get; private set; }

    // The file as it was on the host when it was opened or last saved, to tell whether something else changed it since.
    public byte[] Content { get; private set; }

    public string Saved { get; private set; }

    public (string Name, string Label)? Language { get; }

    public string LanguageLabel => Language?.Label ?? Strings.HostFilePlainText;

    public string LineEnding => Saved.Contains("\r\n", StringComparison.Ordinal) ? "CRLF" : "LF";

    public string StatusRight => Strings.Format(Strings.HostFileStatus, LanguageLabel, LineEnding, Caret);

    public string StatusLeft => Problem ?? (IsSaving ? Strings.Format(Strings.HostFileSaving, Host) : IsDirty ? Strings.HostFileEdited : string.Empty);

    public Action<ServerFileViewModel>? CloseRequested { get; set; }

    public Action<ServerFileViewModel>? SaveRequested { get; set; }

    [ObservableProperty]
    public partial string Text { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccessibleName), nameof(IsUnsaved), nameof(StatusLeft))]
    [NotifyCanExecuteChangedFor(nameof(RevertCommand), nameof(SaveCommand))]
    public partial bool IsDirty { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusLeft))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsSaving { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusLeft))]
    public partial string? Problem { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusRight))]
    public partial string Caret { get; set; } = Strings.Format(Strings.HostFileCaret, 1, 1);

    // Decoded strictly: a file that isn't UTF-8 text would be changed by saving it back, so it isn't opened.
    public static string? ProblemWith(byte[] content, bool isTruncated)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (isTruncated || content.Length > MaxBytes)
        {
            return Strings.HostFileTooLarge;
        }

        if (content.AsSpan().Contains((byte)0))
        {
            return Strings.HostFileBinary;
        }

        try
        {
            _ = s_strict.GetCharCount(content);
            return null;
        }
        catch (DecoderFallbackException)
        {
            return Strings.HostFileNotUtf8;
        }
    }

    // What saving writes: the text, with the byte order mark the file had.
    public byte[] Encode(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var bytes = Encoding.UTF8.GetBytes(text);
        return _hasBom ? [.. Encoding.UTF8.Preamble, .. bytes] : bytes;
    }

    public void MarkSaved(string text, byte[] content, bool needsSudo)
    {
        Saved = text;
        Content = content;
        NeedsSudo = needsSudo;
        IsSaving = false;
        Problem = null;
        IsDirty = !string.Equals(Text, Saved, StringComparison.Ordinal);
        OnPropertyChanged(nameof(LineEnding));
        OnPropertyChanged(nameof(StatusRight));
    }

    // The file as it is on the host now, replacing the user's changes.
    public void Reload(byte[] content, bool needsSudo)
    {
        ArgumentNullException.ThrowIfNull(content);
        var text = Decode(content);
        Text = text;
        MarkSaved(text, content, needsSudo);
    }

    private static string Decode(byte[] content)
    {
        var skip = content.AsSpan().StartsWith(Encoding.UTF8.Preamble) ? 3 : 0;
        return s_strict.GetString(content, skip, content.Length - skip);
    }

    partial void OnTextChanged(string value)
    {
        IsDirty = !string.Equals(value, Saved, StringComparison.Ordinal);
        if (!IsDirty)
        {
            return;
        }

        Problem = null;
    }

    [RelayCommand]
    private void Close() => CloseRequested?.Invoke(this);

    [RelayCommand(CanExecute = nameof(IsDirty))]
    private void Revert() => Text = Saved;

    private bool CanSave() => IsDirty && !IsSaving;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save() => SaveRequested?.Invoke(this);
}
