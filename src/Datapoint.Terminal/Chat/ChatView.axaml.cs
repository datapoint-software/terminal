using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace Datapoint.Terminal.Chat;

public sealed partial class ChatView : UserControl
{
    private bool _isAtEnd = true;

    public ChatView()
    {
        InitializeComponent();

        // Follow the conversation as it grows, unless the user has scrolled up to read.
        Scroller.ScrollChanged += (_, e) =>
        {
            if (e.ExtentDelta.Y > 0 && _isAtEnd)
            {
                Scroller.ScrollToEnd();
                return;
            }

            _isAtEnd = Scroller.Offset.Y + Scroller.Viewport.Height >= Scroller.Extent.Height - 4;
        };

        QuestionCard.AddHandler(KeyDownEvent, OnQuestionKeyDown);

        // After "Something else…" the box is where the user types next; an open question doesn't take focus from the terminal.
        QuestionWriting.PropertyChanged += (_, e) =>
        {
            if (!e.Property.Equals(IsVisibleProperty) || e.NewValue is not true || QuestionWriting.DataContext is not QuestionViewModel { CanGoBack: true })
            {
                return;
            }

            Dispatcher.UIThread.Post(() => QuestionAnswerBox.Focus());
        };
    }

    // Number keys pick a choice, but only while the card has focus, so typing in the terminal or the message box never answers.
    private void OnQuestionKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || DataContext is not ChatViewModel { Question: { } question })
        {
            return;
        }

        var number = e.Key switch
        {
            >= Key.D1 and <= Key.D9 => e.Key - Key.D1 + 1,
            >= Key.NumPad1 and <= Key.NumPad9 => e.Key - Key.NumPad1 + 1,
            _ => 0,
        };
        e.Handled = question.ChooseNumber(number);
    }

    // The caret goes to the end, after any text the menu put there for the user to finish.
    public void FocusInput()
    {
        DraftBox.Focus();
        DraftBox.CaretIndex = DraftBox.Text?.Length ?? 0;
    }

    // Scrolls the step into view and outlines it for a moment.
    public void Reveal(ChatItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ItemList.ContainerFromItem(item)?.BringIntoView();
        item.IsHighlighted = true;
        Avalonia.Threading.DispatcherTimer.RunOnce(() => item.IsHighlighted = false, TimeSpan.FromSeconds(2));
    }
}
