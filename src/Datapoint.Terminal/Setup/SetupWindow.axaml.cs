using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace Datapoint.Terminal.Setup;

// Product decision 146. Closing the window, with its close button or Escape, sets the rest of setup aside.
public sealed partial class SetupWindow : Window
{
    public SetupWindow()
    {
        InitializeComponent();
        for (var index = 0; index < SetupViewModel.StepCount; index++)
        {
            Progress.Children.Add(new Border { Classes = { "progress" } });
        }
    }

    private SetupViewModel? Model => DataContext as SetupViewModel;

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (Model is not { } model)
        {
            return;
        }

        model.Done += (_, _) => Close();
        model.PropertyChanged += OnModelChanged;
        ShowProgress();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        NextButton.Focus();
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        Model?.Abandon();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key != Key.Escape || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        Close();
        e.Handled = true;
    }

    private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.Equals(e.PropertyName, nameof(SetupViewModel.Step), StringComparison.Ordinal))
        {
            return;
        }

        ShowProgress();

        // Focus moves into the new step, so a keyboard user starts from its first choice; it waits for the step to show.
        Dispatcher.UIThread.Post(() => FirstControl()?.Focus(), DispatcherPriority.Loaded);
    }

    private Control? FirstControl() => Model switch
    {
        { IsProvider: true } => Choices.ContainerFromIndex(Math.Max(0, Choices.SelectedIndex)) ?? Choices,
        { IsAppearance: true } => WindowThemes.ContainerFromIndex(Math.Max(0, WindowThemes.SelectedIndex)) ?? WindowThemes,
        _ => NextButton,
    };

    private void ShowProgress()
    {
        var step = Model?.Step ?? 0;
        for (var index = 0; index < Progress.Children.Count; index++)
        {
            Progress.Children[index].Classes.Set("done", index <= step);
        }
    }
}
