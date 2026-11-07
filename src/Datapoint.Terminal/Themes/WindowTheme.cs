namespace Datapoint.Terminal.Themes;

// The colours of the window around the terminals, chosen apart from the terminal theme.
public enum WindowTheme
{
    // Follows the operating system's light or dark appearance.
    System,
    Light,
    Dark,

    // Takes the terminal theme's background and text colours.
    Terminal,
}
