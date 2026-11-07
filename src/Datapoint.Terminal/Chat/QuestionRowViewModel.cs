namespace Datapoint.Terminal.Chat;

// One row in a question's list of choices, with the number key that picks it.
public sealed class QuestionRowViewModel(int number, string label, string? description, bool isRecommended)
{
    public int Number { get; } = number;

    public string NumberText => Number.ToString(System.Globalization.CultureInfo.CurrentCulture);

    public string Label { get; } = label;

    public string? Description { get; } = description;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public bool IsRecommended { get; } = isRecommended;

    // "Something else…", which opens a box for the user's own answer.
    public bool IsOther { get; init; }
}
