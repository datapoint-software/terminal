using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Datapoint.Terminal.Agent.Workspaces;
using Datapoint.Terminal.Resources;

namespace Datapoint.Terminal.Shell;

// Add a Host: a host the workspace lists for everyone, by address or by an alias from the user's SSH config.
public sealed partial class NewHostWindow : Window
{
    private const int MostAliases = 12;

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
        EnvironmentList.ItemsSource = new[]
        {
            Strings.NewHostEnvironmentNone,
            Strings.NewHostEnvironmentDevelopment,
            Strings.NewHostEnvironmentStaging,
            Strings.NewHostEnvironmentProduction,
        };
        EnvironmentList.SelectionChanged += (_, _) => ProductionNote.IsVisible = NewHostForm.Environments.ElementAtOrDefault(EnvironmentList.SelectedIndex) is { } label && Agent.EnvironmentLabels.IsProduction(label);
        foreach (var alias in aliases.Take(MostAliases))
        {
            var chip = new Button { Content = alias, Classes = { "chip" } };
            AutomationProperties.SetName(chip, Strings.Format(Strings.NewHostFromConfigItem, alias));
            chip.Click += (_, _) => UseAlias(alias);
            AliasPanel.Children.Add(chip);
        }

        AliasPanel.IsVisible = aliases.Count > 0;
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
        Opened += (_, _) => NameBox.Focus();
    }

    public static Task<WorkspaceConnection?> AskAsync(Window owner, IReadOnlyCollection<WorkspaceConnection> existing, IReadOnlyList<string> aliases)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return new NewHostWindow(new NewHostForm(existing, aliases), aliases).ShowDialog<WorkspaceConnection?>(owner);
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
    }
}
