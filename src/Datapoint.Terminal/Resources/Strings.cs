using System.Globalization;
using System.Resources;

namespace Datapoint.Terminal.Resources;

/// <summary>The application's text, from Strings.resx. Written by hand: the designer generator only runs inside Visual Studio.</summary>
public static class Strings
{
    private static readonly ResourceManager s_resources = new("Datapoint.Terminal.Resources.Strings", typeof(Strings).Assembly);

    public static string ChatTitle => Get(nameof(ChatTitle));

    public static string ChatInputPlaceholder => Get(nameof(ChatInputPlaceholder));

    public static string ChatSend => Get(nameof(ChatSend));

    public static string ChatStop => Get(nameof(ChatStop));

    public static string ChatContinue => Get(nameof(ChatContinue));

    public static string ChatClear => Get(nameof(ChatClear));

    public static string ChatMode => Get(nameof(ChatMode));

    public static string ModeAsk => Get(nameof(ModeAsk));

    public static string ModeAskForRisky => Get(nameof(ModeAskForRisky));

    public static string ModeAuto => Get(nameof(ModeAuto));

    public static string StatusIdle => Get(nameof(StatusIdle));

    public static string StatusThinking => Get(nameof(StatusThinking));

    public static string StatusRunningCommand => Get(nameof(StatusRunningCommand));

    public static string StatusWaitingForApproval => Get(nameof(StatusWaitingForApproval));

    public static string StatusWaitingForUser => Get(nameof(StatusWaitingForUser));

    public static string StatusInterrupted => Get(nameof(StatusInterrupted));

    public static string StatusNoProvider => Get(nameof(StatusNoProvider));

    public static string ApprovalHeading => Get(nameof(ApprovalHeading));

    public static string ApprovalHeadingTrust => Get(nameof(ApprovalHeadingTrust));

    public static string ApprovalRiskReadOnly => Get(nameof(ApprovalRiskReadOnly));

    public static string ApprovalRiskChanges => Get(nameof(ApprovalRiskChanges));

    public static string ApprovalRiskAlwaysAsk => Get(nameof(ApprovalRiskAlwaysAsk));

    public static string ApprovalCommandLabel => Get(nameof(ApprovalCommandLabel));

    public static string ApprovalRun => Get(nameof(ApprovalRun));

    public static string ApprovalDecline => Get(nameof(ApprovalDecline));

    public static string ApprovalFeedbackPlaceholder => Get(nameof(ApprovalFeedbackPlaceholder));

    public static string QuestionAnswer => Get(nameof(QuestionAnswer));

    public static string QuestionSkip => Get(nameof(QuestionSkip));

    public static string QuestionPlaceholder => Get(nameof(QuestionPlaceholder));

    public static string CommandExitCode => Get(nameof(CommandExitCode));

    public static string CommandRunning => Get(nameof(CommandRunning));

    public static string CommandStillRunning => Get(nameof(CommandStillRunning));

    public static string CommandFinished => Get(nameof(CommandFinished));

    public static string NoticeNoKey => Get(nameof(NoticeNoKey));

    public static string NoticeSentTo => Get(nameof(NoticeSentTo));

    public static string NoticeLocal => Get(nameof(NoticeLocal));

    public static string NoticeProviderFailed => Get(nameof(NoticeProviderFailed));

    public static string AgentLabel => Get(nameof(AgentLabel));

    public static string YouLabel => Get(nameof(YouLabel));

    public static string ModeAskHelp => Get(nameof(ModeAskHelp));

    public static string ModeAskForRiskyHelp => Get(nameof(ModeAskForRiskyHelp));

    public static string ModeAutoHelp => Get(nameof(ModeAutoHelp));

    public static string Format(string format, params object?[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);

    private static string Get(string name) => s_resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;
}
