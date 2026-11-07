using Avalonia.Platform.Storage;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Sessions.Ssh;
using Datapoint.Terminal.Sessions.Ssh.Channels;

namespace Datapoint.Terminal.Shell;

// Product decision 46, file transfer: the user moves a file between this computer and the selected SSH tab's host. The file's
// content never reaches the agent or the provider; the chat notes the transfer for the user's own record.
public sealed partial class MainWindow
{
    private async Task UploadFileAsync()
    {
        if (TransferTab() is not { Client: { } client } tab)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = Strings.PickerUpload, AllowMultiple = false }).ConfigureAwait(true);
        if (files.Count == 0)
        {
            return;
        }

        var file = files[0];
        if (await AskRemotePathAsync(tab, Strings.Format(Strings.UploadTitle, file.Name), Strings.Format(Strings.UploadMessage, tab.ConnectionName), "~/" + file.Name).ConfigureAwait(true) is not { } path)
        {
            return;
        }

        if (await TryUploadAsync(tab, client, file, path, overwrite: false).ConfigureAwait(true)
            || !await ConfirmReplaceAsync(path).ConfigureAwait(true))
        {
            return;
        }

        await TryUploadAsync(tab, client, file, path, overwrite: true).ConfigureAwait(true);
    }

    // False only when the file is already there, so the caller can ask before replacing it.
    private async Task<bool> TryUploadAsync(TerminalTab tab, SshClient client, IStorageFile file, string path, bool overwrite)
    {
        _chat.AddNotice(Strings.Format(Strings.NoticeUploading, file.Name, tab.Name, path));
        try
        {
            var source = await file.OpenReadAsync().ConfigureAwait(true);
            await using (source.ConfigureAwait(true))
            {
                var bytes = await client.UploadAsync(source, path, overwrite, progress: null, CancellationToken.None).ConfigureAwait(true);
                _chat.AddNotice(Strings.Format(Strings.NoticeUploaded, file.Name, tab.Name, path, Size(bytes)));
            }
        }
        catch (RemoteFileExistsException)
        {
            return false;
        }
        catch (Exception exception) when (IsTransferFailure(exception))
        {
            _chat.AddNotice(Strings.Format(Strings.NoticeTransferFailed, file.Name, exception.Message));
        }

        return true;
    }

    private async Task DownloadFileAsync()
    {
        if (TransferTab() is not { Client: { } client } tab
            || await AskRemotePathAsync(tab, Strings.DownloadTitle, Strings.Format(Strings.DownloadMessage, tab.ConnectionName), "~/").ConfigureAwait(true) is not { } path)
        {
            return;
        }

        var name = path.TrimEnd('/').Split('/')[^1];
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = Strings.PickerDownload, SuggestedFileName = name }).ConfigureAwait(true);
        if (file is null)
        {
            return;
        }

        _chat.AddNotice(Strings.Format(Strings.NoticeDownloading, path, tab.Name));
        try
        {
            var destination = await file.OpenWriteAsync().ConfigureAwait(true);
            long bytes;
            await using (destination.ConfigureAwait(true))
            {
                bytes = await client.DownloadAsync(path, destination, progress: null, CancellationToken.None).ConfigureAwait(true);
            }

            _chat.AddNotice(Strings.Format(Strings.NoticeDownloaded, path, tab.Name, file.Name, Size(bytes)));
        }
        catch (Exception exception) when (IsTransferFailure(exception))
        {
            // A partial file would look like the real one.
            await file.DeleteAsync().ConfigureAwait(true);
            _chat.AddNotice(Strings.Format(Strings.NoticeTransferFailed, path, exception.Message));
        }
    }

    private TerminalTab? TransferTab()
    {
        if (SelectedTab is { Client: not null } tab)
        {
            return tab;
        }

        _chat.AddNotice(Strings.NoticeTransferNeedsSsh);
        return null;
    }

    private async Task<string?> AskRemotePathAsync(TerminalTab tab, string heading, string message, string initial)
    {
        var answers = await PromptWindow.AskAsync(this, heading, message, serverText: null, [(Strings.Format(Strings.TransferPathLabel, tab.ConnectionName), false)], [initial]).ConfigureAwait(true);
        var path = answers?[0].Trim();
        return string.IsNullOrEmpty(path) ? null : path;
    }

    private async Task<bool> ConfirmReplaceAsync(string path) =>
        await ChoiceWindow.AskAsync(this, Strings.ReplaceRemoteTitle, Strings.Format(Strings.ReplaceRemoteMessage, path), Strings.Cancel, Strings.ReplaceRemoteConfirm).ConfigureAwait(true) == 1;

    private static bool IsTransferFailure(Exception exception) =>
        exception is IOException or SshChannelException or SshDisconnectedException or UnauthorizedAccessException or ObjectDisposedException;

    private static string Size(long bytes) => bytes switch
    {
        < 1024 => Strings.Format(Strings.SizeBytes, bytes),
        < 1024 * 1024 => Strings.Format(Strings.SizeKilobytes, Math.Round(bytes / 1024.0, 1)),
        _ => Strings.Format(Strings.SizeMegabytes, Math.Round(bytes / (1024.0 * 1024), 1)),
    };
}
