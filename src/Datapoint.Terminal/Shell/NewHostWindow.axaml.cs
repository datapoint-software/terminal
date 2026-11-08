using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Add a Host: a host the workspace lists for everyone, by address or by an alias from the user's SSH config. The same sheet edits
// a host the workspace lists, all but its name.
public sealed partial class NewHostWindow : Window
{
    private const int MostAliases = 12;
    private const int MostLabels = 16;

    private readonly NewHostForm _form;

    public NewHostWindow()
        : this(new NewHostForm([], []), [])
    {
    }

    public NewHostWindow(NewHostForm form, IReadOnlyList<string> aliases)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(aliases);
        _form = form;
        InitializeComponent();
        string[] offered =
        [
            Strings.NewHostEnvironmentNone,
            Strings.NewHostEnvironmentDevelopment,
            Strings.NewHostEnvironmentStaging,
            Strings.NewHostEnvironmentProduction,
        ];
        EnvironmentList.ItemsSource = form.Choices.Select((choice, index) => index < offered.Length ? offered[index] : choice).ToList();
        EnvironmentList.SelectedIndex = Math.Max(form.EnvironmentIndex, 0);
        if (form.IsEditing)
        {
            ShowHost();
        }

        // Only once the boxes hold the host's values, since showing the notes reads them all back into the form.
        EnvironmentList.SelectionChanged += (_, _) => ShowNotes();
        AddressBox.TextChanged += (_, _) => ShowNotes();
        foreach (var alias in aliases.Take(MostAliases))
        {
            var chip = new Button { Content = alias, Classes = { "chip" } };
            AutomationProperties.SetName(chip, Strings.Format(Strings.NewHostFromConfigItem, alias));
            chip.Click += (_, _) => UseAlias(alias);
            AliasPanel.Children.Add(chip);
        }

        AliasPanel.IsVisible = aliases.Count > 0;
        foreach (var label in form.UsedLabels.Take(MostLabels))
        {
            var chip = new Button { Content = label, Classes = { "chip" } };
            AutomationProperties.SetName(chip, Strings.Format(Strings.NewHostLabelsItem, label));
            chip.Click += (_, _) => AddLabel(label);
            LabelPanel.Children.Add(chip);
        }

        LabelPanel.IsVisible = form.UsedLabels.Count > 0;
        AddButton.Click += (_, _) => Add();
        CancelButton.Click += (_, _) => Close(null);
        KeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape)
            {
                return;
            }

            e.Handled = true;
            Close(null);
        };
        Opened += (_, _) => (form.IsEditing ? AddressBox : NameBox).Focus();
        ShowNotes();
    }

    public static Task<WorkspaceConnection?> AskAsync(Window owner, IReadOnlyCollection<WorkspaceConnection> existing, IReadOnlyList<string> aliases)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return new NewHostWindow(new NewHostForm(existing, aliases), aliases).ShowDialog<WorkspaceConnection?>(owner);
    }

    // Offers the SSH config's other aliases as chips, while every alias, the host's own included, counts as one when typed.
    public static Task<WorkspaceConnection?> EditAsync(Window owner, WorkspaceConnection host, IReadOnlyCollection<WorkspaceConnection> existing, IReadOnlyList<string> aliases, IReadOnlyCollection<string> allAliases)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return new NewHostWindow(new NewHostForm(existing, allAliases, host), aliases).ShowDialog<WorkspaceConnection?>(owner);
    }

    private void ShowHost()
    {
        Title = Strings.EditHostTitle;
        TitleText.Text = Strings.Format(Strings.EditHostHeading, _form.Name);
        AddButton.Content = Strings.EditHostSave;
        // Shown as text rather than a box, so it doesn't look like something to type into.
        NameBox.IsReadOnly = true;
        NameBox.Focusable = false;
        NameBox.Background = Avalonia.Media.Brushes.Transparent;
        NameBox.BorderThickness = default;
        ToolTip.SetTip(NameBox, Strings.EditHostNameFixed);
        NameBox.Text = _form.Name;
        AddressBox.Text = _form.Address;
        UserBox.Text = _form.User;
        PortBox.Text = _form.Port;
        LabelsBox.Text = _form.Labels;
    }

    private void ShowNotes()
    {
        Read();
        ProductionNote.IsVisible = _form.IsProduction && !(_form.Editing is { } host && Agent.EnvironmentLabels.IsProduction(host.Environment));
        LeavingProductionNote.IsVisible = _form.IsLeavingProduction;
        MovingNote.IsVisible = _form.IsMoving;
    }

    private void UseAlias(string alias)
    {
        Read();
        _form.UseAlias(alias);
        NameBox.Text = _form.Name;
        AddressBox.Text = _form.Address;
        Problem.IsVisible = false;
        AddressBox.Focus();
    }

    private void AddLabel(string label)
    {
        Read();
        _form.AddLabel(label);
        LabelsBox.Text = _form.Labels;
        LabelsBox.CaretIndex = LabelsBox.Text.Length;
        LabelsBox.Focus();
    }

    private void Add()
    {
        Read();
        var (connection, problem) = _form.Build();
        Problem.Text = problem;
        Problem.IsVisible = problem is not null;
        if (connection is null)
        {
            return;
        }

        Close(connection);
    }

    private void Read()
    {
        _form.Name = NameBox.Text ?? string.Empty;
        _form.Address = AddressBox.Text ?? string.Empty;
        _form.User = UserBox.Text ?? string.Empty;
        _form.Port = PortBox.Text ?? string.Empty;
        _form.EnvironmentIndex = Math.Max(EnvironmentList.SelectedIndex, 0);
        _form.Labels = LabelsBox.Text ?? string.Empty;
    }
}
