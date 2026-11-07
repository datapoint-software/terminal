using System.Globalization;
using System.Resources;

namespace Datapoint.Terminal.Resources;

// Written by hand: the designer generator only runs inside Visual Studio.
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

    public static string MarkdownCopy => Get(nameof(MarkdownCopy));

    public static string MarkdownCopied => Get(nameof(MarkdownCopied));

    public static string MarkdownCopyCode => Get(nameof(MarkdownCopyCode));

    public static string MarkdownSelectAll => Get(nameof(MarkdownSelectAll));

    public static string MarkdownOpenLinkQuestion => Get(nameof(MarkdownOpenLinkQuestion));

    public static string MarkdownOpen => Get(nameof(MarkdownOpen));

    public static string MarkdownCancel => Get(nameof(MarkdownCancel));

    public static string MarkdownLoadImage => Get(nameof(MarkdownLoadImage));

    public static string MarkdownLoadImageHelp => Get(nameof(MarkdownLoadImageHelp));

    public static string MarkdownImageFailed => Get(nameof(MarkdownImageFailed));

    public static string ChatModel => Get(nameof(ChatModel));

    public static string MenuFile => Get(nameof(MenuFile));

    public static string MenuNewProject => Get(nameof(MenuNewProject));

    public static string MenuOpenProject => Get(nameof(MenuOpenProject));

    public static string MenuOpenRecent => Get(nameof(MenuOpenRecent));

    public static string MenuClearRecent => Get(nameof(MenuClearRecent));

    public static string MenuSettings => Get(nameof(MenuSettings));

    public static string MenuCloseWindow => Get(nameof(MenuCloseWindow));

    public static string PickerNewProject => Get(nameof(PickerNewProject));

    public static string PickerOpenProject => Get(nameof(PickerOpenProject));

    public static string NoticeProjectReadOnly => Get(nameof(NoticeProjectReadOnly));

    public static string NoticeProjectFailed => Get(nameof(NoticeProjectFailed));

    public static string NoticeProjectIsNotNew => Get(nameof(NoticeProjectIsNotNew));

    public static string WindowTitle => Get(nameof(WindowTitle));

    public static string WindowTitleWithShell => Get(nameof(WindowTitleWithShell));

    public static string CommandInTerminal => Get(nameof(CommandInTerminal));

    public static string ApprovalInTerminal => Get(nameof(ApprovalInTerminal));

    public static string ApprovalInTerminalEnvironment => Get(nameof(ApprovalInTerminalEnvironment));

    public static string MenuTerminal => Get(nameof(MenuTerminal));

    public static string MenuNewTab => Get(nameof(MenuNewTab));

    public static string MenuCloseTab => Get(nameof(MenuCloseTab));

    public static string MenuNextTab => Get(nameof(MenuNextTab));

    public static string MenuPreviousTab => Get(nameof(MenuPreviousTab));

    public static string TabNew => Get(nameof(TabNew));

    public static string TabClose => Get(nameof(TabClose));

    public static string TabLocal => Get(nameof(TabLocal));

    public static string TabAgentActing => Get(nameof(TabAgentActing));

    public static string TabAgentNeedsUser => Get(nameof(TabAgentNeedsUser));

    public static string TabsName => Get(nameof(TabsName));

    public static string NoticeShellFailed => Get(nameof(NoticeShellFailed));

    public static string NewTabLocal => Get(nameof(NewTabLocal));

    public static string NewTabSshHosts => Get(nameof(NewTabSshHosts));

    public static string NewTabNoSshHosts => Get(nameof(NewTabNoSshHosts));

    public static string AddHostTitle => Get(nameof(AddHostTitle));

    public static string AddHostMessage => Get(nameof(AddHostMessage));

    public static string AddHostAdd => Get(nameof(AddHostAdd));

    public static string AddHostOpenOnce => Get(nameof(AddHostOpenOnce));

    public static string Cancel => Get(nameof(Cancel));

    public static string NoticeProjectReadOnlyNotAdded => Get(nameof(NoticeProjectReadOnlyNotAdded));

    public static string NoticeHostAdded => Get(nameof(NoticeHostAdded));

    public static string PromptFromServer => Get(nameof(PromptFromServer));

    public static string PromptLogIn => Get(nameof(PromptLogIn));

    public static string PasswordTitle => Get(nameof(PasswordTitle));

    public static string PasswordMessage => Get(nameof(PasswordMessage));

    public static string PasswordField => Get(nameof(PasswordField));

    public static string KeyboardInteractiveTitle => Get(nameof(KeyboardInteractiveTitle));

    public static string KeyboardInteractiveMessage => Get(nameof(KeyboardInteractiveMessage));

    public static string HostKeyNewTitle => Get(nameof(HostKeyNewTitle));

    public static string HostKeyNewMessage => Get(nameof(HostKeyNewMessage));

    public static string HostKeyNewOtherTypes => Get(nameof(HostKeyNewOtherTypes));

    public static string HostKeyDetails => Get(nameof(HostKeyDetails));

    public static string HostKeyCancel => Get(nameof(HostKeyCancel));

    public static string HostKeyOnce => Get(nameof(HostKeyOnce));

    public static string HostKeyTrust => Get(nameof(HostKeyTrust));

    public static string HostKeyChangedTitle => Get(nameof(HostKeyChangedTitle));

    public static string HostKeyChangedMessage => Get(nameof(HostKeyChangedMessage));

    public static string HostKeyRevokedTitle => Get(nameof(HostKeyRevokedTitle));

    public static string HostKeyRevokedMessage => Get(nameof(HostKeyRevokedMessage));

    public static string Close => Get(nameof(Close));

    public static string NoticeConnecting => Get(nameof(NoticeConnecting));

    public static string NoticeServerBanner => Get(nameof(NoticeServerBanner));

    public static string NoticeConnectFailed => Get(nameof(NoticeConnectFailed));

    public static string NoticeLoginNote => Get(nameof(NoticeLoginNote));

    public static string MenuProjectTrust => Get(nameof(MenuProjectTrust));

    public static string TrustTitle => Get(nameof(TrustTitle));

    public static string TrustMessage => Get(nameof(TrustMessage));

    public static string TrustFiles => Get(nameof(TrustFiles));

    public static string TrustConnections => Get(nameof(TrustConnections));

    public static string TrustNothing => Get(nameof(TrustNothing));

    public static string TrustNew => Get(nameof(TrustNew));

    public static string TrustDont => Get(nameof(TrustDont));

    public static string TrustTrust => Get(nameof(TrustTrust));

    public static string NoticeInstructions => Get(nameof(NoticeInstructions));

    public static string NoticeInstructionsData => Get(nameof(NoticeInstructionsData));

    public static string NoticeInstructionsCut => Get(nameof(NoticeInstructionsCut));

    public static string NoticeInstructionsUnreadable => Get(nameof(NoticeInstructionsUnreadable));

    public static string GitIgnoreTitle => Get(nameof(GitIgnoreTitle));

    public static string GitIgnoreMessage => Get(nameof(GitIgnoreMessage));

    public static string GitIgnoreTracked => Get(nameof(GitIgnoreTracked));

    public static string GitIgnoreDont => Get(nameof(GitIgnoreDont));

    public static string GitIgnoreAdd => Get(nameof(GitIgnoreAdd));

    public static string NoticeGitIgnoreAdded => Get(nameof(NoticeGitIgnoreAdded));

    public static string NoticeGitIgnoreStillNot => Get(nameof(NoticeGitIgnoreStillNot));

    public static string NoticeGitIgnoreFailed => Get(nameof(NoticeGitIgnoreFailed));

    public static string NoticeGitTracked => Get(nameof(NoticeGitTracked));

    public static string ReviewTitle => Get(nameof(ReviewTitle));

    public static string ReviewMessage => Get(nameof(ReviewMessage));

    public static string ReviewKeep => Get(nameof(ReviewKeep));

    public static string ReviewAccept => Get(nameof(ReviewAccept));

    public static string ReviewNewConnection => Get(nameof(ReviewNewConnection));

    public static string ReviewChange => Get(nameof(ReviewChange));

    public static string ReviewNone => Get(nameof(ReviewNone));

    public static string ReviewDefault => Get(nameof(ReviewDefault));

    public static string NoticeConnectionsStricter => Get(nameof(NoticeConnectionsStricter));

    public static string NoticeConnectionsHeld => Get(nameof(NoticeConnectionsHeld));

    public static string NoticeInstructionsChanged => Get(nameof(NoticeInstructionsChanged));

    public static string ShowChanges => Get(nameof(ShowChanges));

    public static string InstructionChangesTitle => Get(nameof(InstructionChangesTitle));

    public static string InstructionChangesMessage => Get(nameof(InstructionChangesMessage));

    public static string BusyAgent => Get(nameof(BusyAgent));

    public static string BusyFullScreen => Get(nameof(BusyFullScreen));

    public static string BusyCommand => Get(nameof(BusyCommand));

    public static string BusyRemote => Get(nameof(BusyRemote));

    public static string CloseTabTitle => Get(nameof(CloseTabTitle));

    public static string CloseTabMessage => Get(nameof(CloseTabMessage));

    public static string CloseTabConfirm => Get(nameof(CloseTabConfirm));

    public static string CloseWindowTitle => Get(nameof(CloseWindowTitle));

    public static string CloseWindowMessage => Get(nameof(CloseWindowMessage));

    public static string CloseWindowConfirm => Get(nameof(CloseWindowConfirm));

    public static string CloseTabLine => Get(nameof(CloseTabLine));

    public static string QuitTitle => Get(nameof(QuitTitle));

    public static string QuitMessage => Get(nameof(QuitMessage));

    public static string QuitConfirm => Get(nameof(QuitConfirm));

    public static string MarkdownLinks => Get(nameof(MarkdownLinks));

    public static string MarkdownLinkName => Get(nameof(MarkdownLinkName));

    public static string WindowTitleNeedsUser => Get(nameof(WindowTitleNeedsUser));

    public static string NoticeSyncFolder => Get(nameof(NoticeSyncFolder));

    public static string SecretTitle => Get(nameof(SecretTitle));

    public static string SecretMessage => Get(nameof(SecretMessage));

    public static string SecretSkip => Get(nameof(SecretSkip));

    public static string SecretAsWritten => Get(nameof(SecretAsWritten));

    public static string SecretNotSecret => Get(nameof(SecretNotSecret));

    public static string SecretMasked => Get(nameof(SecretMasked));

    public static string SecretLine => Get(nameof(SecretLine));

    public static string SecretMore => Get(nameof(SecretMore));

    public static string NoticeTerminalIgnored => Get(nameof(NoticeTerminalIgnored));

    public static string NoticeFilesSkipped => Get(nameof(NoticeFilesSkipped));

    public static string NoticeHistoryUnavailable => Get(nameof(NoticeHistoryUnavailable));

    public static string ApprovalHeadingOpen => Get(nameof(ApprovalHeadingOpen));

    public static string ApprovalRiskOpen => Get(nameof(ApprovalRiskOpen));

    public static string ApprovalOpen => Get(nameof(ApprovalOpen));

    public static string ApprovalForConnection => Get(nameof(ApprovalForConnection));

    public static string ApprovalForConnectionEnvironment => Get(nameof(ApprovalForConnectionEnvironment));

    public static string NoticeAgentConnectionGone => Get(nameof(NoticeAgentConnectionGone));

    public static string NoticeInstructionsSecretChanged => Get(nameof(NoticeInstructionsSecretChanged));

    public static string ApprovalHeadingIgnored => Get(nameof(ApprovalHeadingIgnored));

    public static string ApprovalRiskIgnored => Get(nameof(ApprovalRiskIgnored));

    public static string NoticeMemory => Get(nameof(NoticeMemory));

    public static string MenuOpenGlobalMemory => Get(nameof(MenuOpenGlobalMemory));

    public static string MenuOpenProjectMemory => Get(nameof(MenuOpenProjectMemory));

    public static string MenuClearProjectMemory => Get(nameof(MenuClearProjectMemory));

    public static string NoticeMemoryUnwritable => Get(nameof(NoticeMemoryUnwritable));

    public static string ClearMemoryTitle => Get(nameof(ClearMemoryTitle));

    public static string ClearMemoryMessage => Get(nameof(ClearMemoryMessage));

    public static string ClearMemoryConfirm => Get(nameof(ClearMemoryConfirm));

    public static string NoticeMemoryCleared => Get(nameof(NoticeMemoryCleared));

    public static string MemoryOpen => Get(nameof(MemoryOpen));

    public static string HostDocsOpen => Get(nameof(HostDocsOpen));

    public static string HistoryTitle => Get(nameof(HistoryTitle));

    public static string HistorySearch => Get(nameof(HistorySearch));

    public static string HistoryKeep => Get(nameof(HistoryKeep));

    public static string HistoryKeepForever => Get(nameof(HistoryKeepForever));

    public static string HistoryKeepYear => Get(nameof(HistoryKeepYear));

    public static string HistoryKeepDays => Get(nameof(HistoryKeepDays));

    public static string HistoryClear => Get(nameof(HistoryClear));

    public static string HistoryList => Get(nameof(HistoryList));

    public static string HistoryEntries => Get(nameof(HistoryEntries));

    public static string HistoryEmpty => Get(nameof(HistoryEmpty));

    public static string HistoryNoMatches => Get(nameof(HistoryNoMatches));

    public static string HistoryYou => Get(nameof(HistoryYou));

    public static string HistoryAgent => Get(nameof(HistoryAgent));

    public static string HistoryCommand => Get(nameof(HistoryCommand));

    public static string HistoryCommandExit => Get(nameof(HistoryCommandExit));

    public static string HistoryNotice => Get(nameof(HistoryNotice));

    public static string HistoryShortenTitle => Get(nameof(HistoryShortenTitle));

    public static string HistoryShortenMessage => Get(nameof(HistoryShortenMessage));

    public static string HistoryShortenConfirm => Get(nameof(HistoryShortenConfirm));

    public static string HistoryClearTitle => Get(nameof(HistoryClearTitle));

    public static string HistoryClearMessage => Get(nameof(HistoryClearMessage));

    public static string HistoryClearConnection => Get(nameof(HistoryClearConnection));

    public static string HistoryClearAll => Get(nameof(HistoryClearAll));

    public static string HistoryClearedAll => Get(nameof(HistoryClearedAll));

    public static string HistoryClearedConnection => Get(nameof(HistoryClearedConnection));

    public static string HistoryProblem => Get(nameof(HistoryProblem));

    public static string MenuShowHistory => Get(nameof(MenuShowHistory));

    public static string ApprovalSend => Get(nameof(ApprovalSend));

    public static string ApprovalHeadingKeys => Get(nameof(ApprovalHeadingKeys));

    public static string ApprovalRiskKeys => Get(nameof(ApprovalRiskKeys));

    public static string ProviderContextWindow => Get(nameof(ProviderContextWindow));

    public static string ProviderContextWindowHint => Get(nameof(ProviderContextWindowHint));

    public static string ProviderOutputLimit => Get(nameof(ProviderOutputLimit));

    public static string ProviderOutputLimitHint => Get(nameof(ProviderOutputLimitHint));

    public static string SwitchTitle => Get(nameof(SwitchTitle));

    public static string SwitchToCloud => Get(nameof(SwitchToCloud));

    public static string SwitchSmallerContext => Get(nameof(SwitchSmallerContext));

    public static string SwitchConfirm => Get(nameof(SwitchConfirm));

    public static string AutoProductionTitle => Get(nameof(AutoProductionTitle));

    public static string AutoProductionMessage => Get(nameof(AutoProductionMessage));

    public static string AutoProductionConfirm => Get(nameof(AutoProductionConfirm));

    public static string Format(string format, params object?[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);

    private static string Get(string name) => s_resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;
}
