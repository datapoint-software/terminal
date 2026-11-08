using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Datapoint.Terminal.Resources;
using Datapoint.Terminal.Themes;

namespace Datapoint.Terminal.Settings;

public sealed partial class AppearancePage : UserControl
{
    // Theme files are small; anything much larger isn't one.
    private const long MaxThemeBytes = 256 * 1024;

    public AppearancePage()
    {
        InitializeComponent();
        ImportButton.Click += (_, _) => _ = ImportAsync();
        EditButton.Click += (_, _) => _ = EditAsync();
    }

    private AppearanceViewModel? Model => DataContext as AppearanceViewModel;

    private async Task ImportAsync()
    {
        if (TopLevel.GetTopLevel(this) is not { } top || Model is not { } model)
        {
            return;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = Strings.ThemeImportTitle, AllowMultiple = false }).ConfigureAwait(true);
        if (files.Count == 0 || files[0].TryGetLocalPath() is not { } path)
        {
            return;
        }

        var name = Path.GetFileName(path);
        try
        {
            if (new FileInfo(path).Length > MaxThemeBytes)
            {
                Show(Strings.Format(Strings.ThemeImportFailed, name, Strings.ThemeImportTooLarge));
                return;
            }

            var (theme, problem) = ThemeImport.Read(name, await File.ReadAllTextAsync(path).ConfigureAwait(true));
            if (theme is null)
            {
                Show(Strings.Format(Strings.ThemeImportFailed, name, problem));
                return;
            }

            Show(null);
            model.Save(theme);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Show(Strings.Format(Strings.ThemeImportFailed, name, exception.Message.TrimEnd('.')));
        }
    }

    private async Task EditAsync()
    {
        if (TopLevel.GetTopLevel(this) is not Window owner || Model is not { } model)
        {
            return;
        }

        if (await ThemeEditorWindow.EditAsync(owner, model.CurrentTheme).ConfigureAwait(true) is not { } edited)
        {
            return;
        }

        model.Save(edited);
    }

    private void OnRemoveClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: ThemeCard card })
        {
            return;
        }

        Model?.Remove(card);
    }

    private void Show(string? problem)
    {
        ImportStatus.Text = problem;
        ImportStatus.IsVisible = problem is not null;
    }
}
