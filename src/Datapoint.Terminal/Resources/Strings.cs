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

    public static string NoticeProviderFailed => Get(nameof(NoticeProviderFailed));

    public static string ModeAskHelp => Get(nameof(ModeAskHelp));

    public static string ModeAskForRiskyHelp => Get(nameof(ModeAskForRiskyHelp));

    public static string ModeAutoHelp => Get(nameof(ModeAutoHelp));

    public static string SettingsTitle => Get(nameof(SettingsTitle));

    public static string SettingsProviders => Get(nameof(SettingsProviders));

    public static string SettingsAdd => Get(nameof(SettingsAdd));

    public static string SettingsRemove => Get(nameof(SettingsRemove));

    public static string SettingsEmpty => Get(nameof(SettingsEmpty));

    public static string ProviderKind => Get(nameof(ProviderKind));

    public static string ProviderKindAnthropic => Get(nameof(ProviderKindAnthropic));

    public static string ProviderKindOpenAI => Get(nameof(ProviderKindOpenAI));

    public static string ProviderKindCompatible => Get(nameof(ProviderKindCompatible));

    public static string ProviderName => Get(nameof(ProviderName));

    public static string ProviderModel => Get(nameof(ProviderModel));

    public static string ProviderEndpoint => Get(nameof(ProviderEndpoint));

    public static string ProviderEndpointHint => Get(nameof(ProviderEndpointHint));

    public static string ProviderIsLocal => Get(nameof(ProviderIsLocal));

    public static string ProviderApiKey => Get(nameof(ProviderApiKey));

    public static string ProviderApiKeyOptional => Get(nameof(ProviderApiKeyOptional));

    public static string ProviderKeyPlaceholder => Get(nameof(ProviderKeyPlaceholder));

    public static string ProviderKeySave => Get(nameof(ProviderKeySave));

    public static string ProviderKeyReplace => Get(nameof(ProviderKeyReplace));

    public static string ProviderKeyRemove => Get(nameof(ProviderKeyRemove));

    public static string ProviderKeySaved => Get(nameof(ProviderKeySaved));

    public static string ProviderKeyFromEnvironment => Get(nameof(ProviderKeyFromEnvironment));

    public static string ProviderKeyNoStore => Get(nameof(ProviderKeyNoStore));

    public static string ProviderKeyGetOne => Get(nameof(ProviderKeyGetOne));

    public static string ProviderUse => Get(nameof(ProviderUse));

    public static string ProviderInUse => Get(nameof(ProviderInUse));

    public static string ProviderTest => Get(nameof(ProviderTest));

    public static string ProviderTesting => Get(nameof(ProviderTesting));

    public static string ProviderTestPassed => Get(nameof(ProviderTestPassed));

    public static string ProviderTestFailed => Get(nameof(ProviderTestFailed));

    public static string ProviderSendsTo => Get(nameof(ProviderSendsTo));

    public static string ProviderStaysLocal => Get(nameof(ProviderStaysLocal));

    public static string ChatSettings => Get(nameof(ChatSettings));


    public static string ChatConnect => Get(nameof(ChatConnect));

    public static string ChatConnectText => Get(nameof(ChatConnectText));

    public static string NoticeSwitched => Get(nameof(NoticeSwitched));

    public static string Format(string format, params object?[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);

    private static string Get(string name) => s_resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;
}
