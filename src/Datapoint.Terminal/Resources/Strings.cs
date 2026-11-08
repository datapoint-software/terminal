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

    public static string ModePlan => Get(nameof(ModePlan));

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

    public static string ModePlanHelp => Get(nameof(ModePlanHelp));

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


    public static string MenuFile => Get(nameof(MenuFile));

    public static string MenuNewWorkspace => Get(nameof(MenuNewWorkspace));

    public static string MenuOpenWorkspace => Get(nameof(MenuOpenWorkspace));

    public static string MenuOpenRecent => Get(nameof(MenuOpenRecent));

    public static string MenuClearRecent => Get(nameof(MenuClearRecent));

    public static string MenuSettings => Get(nameof(MenuSettings));

    public static string MenuCloseWindow => Get(nameof(MenuCloseWindow));

    public static string PickerNewWorkspace => Get(nameof(PickerNewWorkspace));

    public static string PickerOpenWorkspace => Get(nameof(PickerOpenWorkspace));

    public static string NoticeWorkspaceReadOnly => Get(nameof(NoticeWorkspaceReadOnly));

    public static string NoticeWorkspaceFailed => Get(nameof(NoticeWorkspaceFailed));

    public static string NoticeWorkspaceIsNotNew => Get(nameof(NoticeWorkspaceIsNotNew));

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

    public static string NoticeWorkspaceReadOnlyNotAdded => Get(nameof(NoticeWorkspaceReadOnlyNotAdded));

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

    public static string MenuWorkspaceTrust => Get(nameof(MenuWorkspaceTrust));

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

    public static string ReviewTitle => Get(nameof(ReviewTitle));

    public static string ReviewMessage => Get(nameof(ReviewMessage));

    public static string ReviewKeep => Get(nameof(ReviewKeep));

    public static string ReviewAccept => Get(nameof(ReviewAccept));

    public static string ReviewNewConnection => Get(nameof(ReviewNewConnection));

    public static string ReviewChange => Get(nameof(ReviewChange));

    public static string ReviewNone => Get(nameof(ReviewNone));

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

    public static string NoticeMemoryUnwritable => Get(nameof(NoticeMemoryUnwritable));

    public static string ClearMemoryMessage => Get(nameof(ClearMemoryMessage));

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

    public static string PendingOutput => Get(nameof(PendingOutput));

    public static string PendingOutputTerminal => Get(nameof(PendingOutputTerminal));

    public static string PendingOutputCut => Get(nameof(PendingOutputCut));

    public static string MenuView => Get(nameof(MenuView));

    public static string MenuCollapseTerminals => Get(nameof(MenuCollapseTerminals));

    public static string MenuExpandTerminals => Get(nameof(MenuExpandTerminals));

    public static string FindPlaceholder => Get(nameof(FindPlaceholder));

    public static string FindOlder => Get(nameof(FindOlder));

    public static string FindNewer => Get(nameof(FindNewer));

    public static string FindClose => Get(nameof(FindClose));

    public static string FindNone => Get(nameof(FindNone));

    public static string FindCount => Get(nameof(FindCount));

    public static string MenuFind => Get(nameof(MenuFind));

    public static string MenuBiggerText => Get(nameof(MenuBiggerText));

    public static string MenuSmallerText => Get(nameof(MenuSmallerText));

    public static string MenuActualSize => Get(nameof(MenuActualSize));

    public static string MenuTheme => Get(nameof(MenuTheme));

    public static string MenuThemeSystem => Get(nameof(MenuThemeSystem));

    public static string MenuThemeDark => Get(nameof(MenuThemeDark));

    public static string MenuThemeLight => Get(nameof(MenuThemeLight));

    public static string MenuFont => Get(nameof(MenuFont));

    public static string MenuFontDefault => Get(nameof(MenuFontDefault));

    public static string NotifyFinished => Get(nameof(NotifyFinished));

    public static string NotifyApproval => Get(nameof(NotifyApproval));

    public static string NotifyQuestion => Get(nameof(NotifyQuestion));

    public static string MenuNotifications => Get(nameof(MenuNotifications));

    public static string NoticePanesFull => Get(nameof(NoticePanesFull));

    public static string MenuSplitRight => Get(nameof(MenuSplitRight));

    public static string MenuSplitDown => Get(nameof(MenuSplitDown));

    public static string MenuNextPane => Get(nameof(MenuNextPane));

    public static string MenuPreviousPane => Get(nameof(MenuPreviousPane));

    public static string MenuMovePaneToTab => Get(nameof(MenuMovePaneToTab));

    public static string NoticePaneConnectionGone => Get(nameof(NoticePaneConnectionGone));

    public static string ApprovalHeadingPlan => Get(nameof(ApprovalHeadingPlan));

    public static string ApprovalRiskPlan => Get(nameof(ApprovalRiskPlan));

    public static string ApprovalPlanAnyTerminal => Get(nameof(ApprovalPlanAnyTerminal));

    public static string ApprovalPlanStep => Get(nameof(ApprovalPlanStep));

    public static string ApprovalOutsidePlan => Get(nameof(ApprovalOutsidePlan));

    public static string ApprovalHeadingEdit => Get(nameof(ApprovalHeadingEdit));

    public static string ApprovalApplyEdit => Get(nameof(ApprovalApplyEdit));

    public static string ApprovalRiskEdit => Get(nameof(ApprovalRiskEdit));

    public static string RecordingUserCommand => Get(nameof(RecordingUserCommand));

    public static string RecordingAgentCommand => Get(nameof(RecordingAgentCommand));

    public static string RecordingAgentAction => Get(nameof(RecordingAgentAction));

    public static string NoticeRecordingFailed => Get(nameof(NoticeRecordingFailed));

    public static string MenuRecordSessions => Get(nameof(MenuRecordSessions));

    public static string MenuShowRecordings => Get(nameof(MenuShowRecordings));

    public static string RecordingsTitle => Get(nameof(RecordingsTitle));

    public static string RecordingsList => Get(nameof(RecordingsList));

    public static string RecordingsEmpty => Get(nameof(RecordingsEmpty));

    public static string RecordingsPlay => Get(nameof(RecordingsPlay));

    public static string RecordingsPause => Get(nameof(RecordingsPause));

    public static string RecordingsSpeed => Get(nameof(RecordingsSpeed));

    public static string RecordingsPosition => Get(nameof(RecordingsPosition));

    public static string RecordingsMarkers => Get(nameof(RecordingsMarkers));

    public static string RecordingsDelete => Get(nameof(RecordingsDelete));

    public static string RecordingsDeleteTitle => Get(nameof(RecordingsDeleteTitle));

    public static string RecordingsDeleteMessage => Get(nameof(RecordingsDeleteMessage));

    public static string RecordingsDeleteConfirm => Get(nameof(RecordingsDeleteConfirm));

    public static string RecordingsUnreadable => Get(nameof(RecordingsUnreadable));

    public static string RecordingsNote => Get(nameof(RecordingsNote));

    public static string ApprovalAlsoApproves => Get(nameof(ApprovalAlsoApproves));

    public static string NoticeJumping => Get(nameof(NoticeJumping));

    public static string NoticeForwardUnreadable => Get(nameof(NoticeForwardUnreadable));

    public static string NoticeForwardLoopbackOnly => Get(nameof(NoticeForwardLoopbackOnly));

    public static string NoticeForwarding => Get(nameof(NoticeForwarding));

    public static string NoticeForwardFailed => Get(nameof(NoticeForwardFailed));

    public static string MenuUploadFile => Get(nameof(MenuUploadFile));

    public static string MenuDownloadFile => Get(nameof(MenuDownloadFile));

    public static string PickerUpload => Get(nameof(PickerUpload));

    public static string PickerDownload => Get(nameof(PickerDownload));

    public static string UploadTitle => Get(nameof(UploadTitle));

    public static string UploadMessage => Get(nameof(UploadMessage));

    public static string DownloadTitle => Get(nameof(DownloadTitle));

    public static string DownloadMessage => Get(nameof(DownloadMessage));

    public static string TransferPathLabel => Get(nameof(TransferPathLabel));

    public static string NoticeUploading => Get(nameof(NoticeUploading));

    public static string NoticeUploaded => Get(nameof(NoticeUploaded));

    public static string NoticeDownloading => Get(nameof(NoticeDownloading));

    public static string NoticeDownloaded => Get(nameof(NoticeDownloaded));

    public static string NoticeTransferFailed => Get(nameof(NoticeTransferFailed));

    public static string NoticeTransferNeedsSsh => Get(nameof(NoticeTransferNeedsSsh));

    public static string ReplaceRemoteTitle => Get(nameof(ReplaceRemoteTitle));

    public static string ReplaceRemoteMessage => Get(nameof(ReplaceRemoteMessage));

    public static string ReplaceRemoteConfirm => Get(nameof(ReplaceRemoteConfirm));

    public static string SizeBytes => Get(nameof(SizeBytes));

    public static string SizeKilobytes => Get(nameof(SizeKilobytes));

    public static string SizeMegabytes => Get(nameof(SizeMegabytes));

    public static string ExcerptAttached => Get(nameof(ExcerptAttached));

    public static string ExcerptSent => Get(nameof(ExcerptSent));

    public static string ExcerptRemove => Get(nameof(ExcerptRemove));

    public static string MenuExplainSelection => Get(nameof(MenuExplainSelection));

    public static string MenuFixFailedCommand => Get(nameof(MenuFixFailedCommand));

    public static string NoticeSelectFirst => Get(nameof(NoticeSelectFirst));

    public static string NoticeNoFailedCommand => Get(nameof(NoticeNoFailedCommand));

    public static string ExcerptSelection => Get(nameof(ExcerptSelection));

    public static string ExcerptFailedCommand => Get(nameof(ExcerptFailedCommand));

    public static string ExplainSelectionDraft => Get(nameof(ExplainSelectionDraft));

    public static string FixCommandDraft => Get(nameof(FixCommandDraft));

    public static string ProviderInputPrice => Get(nameof(ProviderInputPrice));

    public static string ProviderOutputPrice => Get(nameof(ProviderOutputPrice));

    public static string ProviderPriceHint => Get(nameof(ProviderPriceHint));

    public static string UsageSession => Get(nameof(UsageSession));

    public static string UsageCost => Get(nameof(UsageCost));

    public static string UsageLimit => Get(nameof(UsageLimit));

    public static string UsageUnreported => Get(nameof(UsageUnreported));

    public static string UsageTerminal => Get(nameof(UsageTerminal));

    public static string MenuSpendingLimit => Get(nameof(MenuSpendingLimit));

    public static string SpendingLimitTitle => Get(nameof(SpendingLimitTitle));

    public static string SpendingLimitMessage => Get(nameof(SpendingLimitMessage));

    public static string SpendingLimitNeedsPrices => Get(nameof(SpendingLimitNeedsPrices));

    public static string SpendingLimitLabel => Get(nameof(SpendingLimitLabel));

    public static string NoticeSpendingLimitUnreadable => Get(nameof(NoticeSpendingLimitUnreadable));

    public static string NoticeSpendingLimitSet => Get(nameof(NoticeSpendingLimitSet));

    public static string NoticeSpendingLimitRemoved => Get(nameof(NoticeSpendingLimitRemoved));

    public static string QuestionRecommended => Get(nameof(QuestionRecommended));

    public static string PlanHazardEndsConnection => Get(nameof(PlanHazardEndsConnection));

    public static string PlanHazardErasesDisk => Get(nameof(PlanHazardErasesDisk));

    public static string PlanHazardDeletesRecursively => Get(nameof(PlanHazardDeletesRecursively));

    public static string PlanHazardChangesTrust => Get(nameof(PlanHazardChangesTrust));

    public static string PlanRollbackTitle => Get(nameof(PlanRollbackTitle));

    public static string PlanChecklistTitle => Get(nameof(PlanChecklistTitle));

    public static string PlanStepPending => Get(nameof(PlanStepPending));

    public static string PlanStepRunning => Get(nameof(PlanStepRunning));

    public static string PlanStepSucceeded => Get(nameof(PlanStepSucceeded));

    public static string PlanStepFailed => Get(nameof(PlanStepFailed));

    public static string PlanStepFinished => Get(nameof(PlanStepFinished));

    public static string PlanStepAccessible => Get(nameof(PlanStepAccessible));

    public static string PlanStepCount => Get(nameof(PlanStepCount));

    public static string PlanStepCountOne => Get(nameof(PlanStepCountOne));

    public static string PlanStepCountWarning => Get(nameof(PlanStepCountWarning));

    public static string PlanStepCountWarnings => Get(nameof(PlanStepCountWarnings));

    public static string PlanProgress => Get(nameof(PlanProgress));

    public static string PlanProgressFailed => Get(nameof(PlanProgressFailed));

    public static string PlanNotStarted => Get(nameof(PlanNotStarted));

    public static string PlanFolded => Get(nameof(PlanFolded));

    public static string PlanFinished => Get(nameof(PlanFinished));

    public static string PlanFinishedFailed => Get(nameof(PlanFinishedFailed));

    public static string PlanStopped => Get(nameof(PlanStopped));

    public static string PlanStoppedFailed => Get(nameof(PlanStoppedFailed));

    public static string PlanStepTag => Get(nameof(PlanStepTag));

    public static string PlanShowSteps => Get(nameof(PlanShowSteps));

    public static string PlanHideSteps => Get(nameof(PlanHideSteps));

    public static string ProviderCachedInputPrice => Get(nameof(ProviderCachedInputPrice));

    public static string ProviderCachedPriceHint => Get(nameof(ProviderCachedPriceHint));

    public static string UsageCached => Get(nameof(UsageCached));

    public static string QuestionHeading => Get(nameof(QuestionHeading));

    public static string QuestionSomethingElse => Get(nameof(QuestionSomethingElse));

    public static string QuestionKeys => Get(nameof(QuestionKeys));

    public static string QuestionBack => Get(nameof(QuestionBack));

    public static string QuestionSend => Get(nameof(QuestionSend));

    public static string QuestionAsked => Get(nameof(QuestionAsked));

    public static string QuestionSkipped => Get(nameof(QuestionSkipped));

    public static string QuestionReplaced => Get(nameof(QuestionReplaced));

    public static string ApprovalReplaced => Get(nameof(ApprovalReplaced));

    public static string ChatInputReplaces => Get(nameof(ChatInputReplaces));

    public static string SidebarHosts => Get(nameof(SidebarHosts));

    public static string SidebarOtherHosts => Get(nameof(SidebarOtherHosts));

    public static string SidebarOpenTerminals => Get(nameof(SidebarOpenTerminals));

    public static string SidebarAdd => Get(nameof(SidebarAdd));

    public static string SidebarOpenTerminal => Get(nameof(SidebarOpenTerminal));

    public static string SidebarOpenDocumentation => Get(nameof(SidebarOpenDocumentation));

    public static string SidebarNoHosts => Get(nameof(SidebarNoHosts));

    public static string MenuHideSidebar => Get(nameof(MenuHideSidebar));

    public static string MenuShowSidebar => Get(nameof(MenuShowSidebar));

    public static string SidebarLabel => Get(nameof(SidebarLabel));

    public static string SidebarAddHost => Get(nameof(SidebarAddHost));

    public static string SidebarNoDocumentation => Get(nameof(SidebarNoDocumentation));

    public static string SidebarReadOnly => Get(nameof(SidebarReadOnly));

    public static string DocumentPlaceholder => Get(nameof(DocumentPlaceholder));

    public static string DocumentConflict => Get(nameof(DocumentConflict));

    public static string DocumentKeepMine => Get(nameof(DocumentKeepMine));

    public static string DocumentTakeTheirs => Get(nameof(DocumentTakeTheirs));

    public static string DocumentSaveFailed => Get(nameof(DocumentSaveFailed));

    public static string DocumentCantOpen => Get(nameof(DocumentCantOpen));

    public static string DocumentClose => Get(nameof(DocumentClose));

    public static string DocumentTabName => Get(nameof(DocumentTabName));

    public static string DocumentTabUnsaved => Get(nameof(DocumentTabUnsaved));

    public static string DocumentTabsName => Get(nameof(DocumentTabsName));

    public static string DocumentOpenTerminal => Get(nameof(DocumentOpenTerminal));

    public static string MenuFormat => Get(nameof(MenuFormat));

    public static string MenuBold => Get(nameof(MenuBold));

    public static string MenuItalic => Get(nameof(MenuItalic));

    public static string MenuInlineCode => Get(nameof(MenuInlineCode));

    public static string MenuLink => Get(nameof(MenuLink));

    public static string MenuShowMarkdown => Get(nameof(MenuShowMarkdown));

    public static string MenuCloseDocument => Get(nameof(MenuCloseDocument));

    public static string MenuCut => Get(nameof(MenuCut));

    public static string MenuCopy => Get(nameof(MenuCopy));

    public static string MenuPaste => Get(nameof(MenuPaste));

    public static string NoticeRemoteIntegration => Get(nameof(NoticeRemoteIntegration));

    public static string NoticeRemoteNoIntegration => Get(nameof(NoticeRemoteNoIntegration));

    public static string WorkspaceHostKeyTitle => Get(nameof(WorkspaceHostKeyTitle));

    public static string WorkspaceHostKeyMessage => Get(nameof(WorkspaceHostKeyMessage));

    public static string WorkspaceHostKeyDetails => Get(nameof(WorkspaceHostKeyDetails));

    public static string WorkspaceHostKeyArchive => Get(nameof(WorkspaceHostKeyArchive));

    public static string WorkspaceHostKeyReview => Get(nameof(WorkspaceHostKeyReview));

    public static string WorkspaceHostKeyKeep => Get(nameof(WorkspaceHostKeyKeep));

    public static string NoticeWorkspaceHostKeyChanged => Get(nameof(NoticeWorkspaceHostKeyChanged));

    public static string NoticeHostDocsArchived => Get(nameof(NoticeHostDocsArchived));

    public static string NoticeHostDocsArchiveFailed => Get(nameof(NoticeHostDocsArchiveFailed));

    public static string MenuSelectWithKeyboard => Get(nameof(MenuSelectWithKeyboard));

    public static string FindPattern => Get(nameof(FindPattern));

    public static string FindInvalidPattern => Get(nameof(FindInvalidPattern));

    public static string ScrollbackDivider => Get(nameof(ScrollbackDivider));

    public static string WhenToday => Get(nameof(WhenToday));

    public static string WhenYesterday => Get(nameof(WhenYesterday));

    public static string WhenOnDay => Get(nameof(WhenOnDay));

    public static string NoticeEarlierWork => Get(nameof(NoticeEarlierWork));

    public static string NoticeEarlierWorkNoReply => Get(nameof(NoticeEarlierWorkNoReply));

    public static string HistoryShowConversation => Get(nameof(HistoryShowConversation));

    public static string SettingsPageProviders => Get(nameof(SettingsPageProviders));

    public static string SettingsPageAppearance => Get(nameof(SettingsPageAppearance));

    public static string SettingsPageHistory => Get(nameof(SettingsPageHistory));

    public static string AppearanceThemeHeading => Get(nameof(AppearanceThemeHeading));

    public static string ThemeDefault => Get(nameof(ThemeDefault));

    public static string ThemeFollowsSystem => Get(nameof(ThemeFollowsSystem));

    public static string ThemeImported => Get(nameof(ThemeImported));

    public static string ThemeCustom => Get(nameof(ThemeCustom));

    public static string ThemeImport => Get(nameof(ThemeImport));

    public static string ThemeEditColors => Get(nameof(ThemeEditColors));

    public static string ThemeRemove => Get(nameof(ThemeRemove));

    public static string ThemeImportHint => Get(nameof(ThemeImportHint));

    public static string AppearanceFont => Get(nameof(AppearanceFont));

    public static string AppearanceTextSize => Get(nameof(AppearanceTextSize));

    public static string AppearanceFontDefault => Get(nameof(AppearanceFontDefault));

    public static string AppearanceTextSizeValue => Get(nameof(AppearanceTextSizeValue));

    public static string ThemeImportTitle => Get(nameof(ThemeImportTitle));

    public static string ThemeImportFailed => Get(nameof(ThemeImportFailed));

    public static string ThemeCardName => Get(nameof(ThemeCardName));

    public static string ThemeEditorTitle => Get(nameof(ThemeEditorTitle));

    public static string ThemeEditorName => Get(nameof(ThemeEditorName));

    public static string ThemeEditorCopyName => Get(nameof(ThemeEditorCopyName));

    public static string ThemeEditorSave => Get(nameof(ThemeEditorSave));

    public static string ThemeEditorInvalid => Get(nameof(ThemeEditorInvalid));

    public static string ThemeColorText => Get(nameof(ThemeColorText));

    public static string ThemeColorBackground => Get(nameof(ThemeColorBackground));

    public static string ThemeColorCursor => Get(nameof(ThemeColorCursor));

    public static string ThemeColorSelection => Get(nameof(ThemeColorSelection));

    public static string ThemeColorNames => Get(nameof(ThemeColorNames));

    public static string HistoryKeepFor => Get(nameof(HistoryKeepFor));

    public static string HistoryKeepHint => Get(nameof(HistoryKeepHint));

    public static string HistoryInWorkspace => Get(nameof(HistoryInWorkspace));

    public static string HistoryUsage => Get(nameof(HistoryUsage));

    public static string HistoryClearRow => Get(nameof(HistoryClearRow));

    public static string HistoryClearRowName => Get(nameof(HistoryClearRowName));

    public static string HistoryClearHint => Get(nameof(HistoryClearHint));

    public static string HistoryNoWorkspace => Get(nameof(HistoryNoWorkspace));

    public static string HistoryNothingKept => Get(nameof(HistoryNothingKept));

    public static string HistoryClearOneTitle => Get(nameof(HistoryClearOneTitle));

    public static string HistoryClearOneMessage => Get(nameof(HistoryClearOneMessage));

    public static string NewHostTitle => Get(nameof(NewHostTitle));

    public static string NewHostCaption => Get(nameof(NewHostCaption));

    public static string NewHostName => Get(nameof(NewHostName));

    public static string NewHostAddress => Get(nameof(NewHostAddress));

    public static string NewHostAddressHint => Get(nameof(NewHostAddressHint));

    public static string NewHostUser => Get(nameof(NewHostUser));

    public static string NewHostPort => Get(nameof(NewHostPort));

    public static string NewHostEnvironment => Get(nameof(NewHostEnvironment));

    public static string NewHostEnvironmentNone => Get(nameof(NewHostEnvironmentNone));

    public static string NewHostEnvironmentDevelopment => Get(nameof(NewHostEnvironmentDevelopment));

    public static string NewHostEnvironmentStaging => Get(nameof(NewHostEnvironmentStaging));

    public static string NewHostEnvironmentProduction => Get(nameof(NewHostEnvironmentProduction));

    public static string NewHostProductionNote => Get(nameof(NewHostProductionNote));

    public static string NewHostFromConfig => Get(nameof(NewHostFromConfig));

    public static string NewHostFromConfigItem => Get(nameof(NewHostFromConfigItem));

    public static string NewHostLogin => Get(nameof(NewHostLogin));

    public static string NewHostAdd => Get(nameof(NewHostAdd));

    public static string NewHostMenu => Get(nameof(NewHostMenu));

    public static string NewHostNameProblem => Get(nameof(NewHostNameProblem));

    public static string NewHostNameTaken => Get(nameof(NewHostNameTaken));

    public static string NewHostAddressProblem => Get(nameof(NewHostAddressProblem));

    public static string NewHostUserProblem => Get(nameof(NewHostUserProblem));

    public static string NewHostPortProblem => Get(nameof(NewHostPortProblem));

    public static string NewHostReadOnly => Get(nameof(NewHostReadOnly));

    public static string HostFileMenu => Get(nameof(HostFileMenu));

    public static string HostFileOpenTitle => Get(nameof(HostFileOpenTitle));

    public static string HostFileOpenHint => Get(nameof(HostFileOpenHint));

    public static string HostFileOpenPath => Get(nameof(HostFileOpenPath));

    public static string HostFileOpenNote => Get(nameof(HostFileOpenNote));

    public static string HostFileOpen => Get(nameof(HostFileOpen));

    public static string HostFileOpenFolderProblem => Get(nameof(HostFileOpenFolderProblem));

    public static string HostFileNoFiles => Get(nameof(HostFileNoFiles));

    public static string HostFileCantOpen => Get(nameof(HostFileCantOpen));

    public static string HostFileTooLarge => Get(nameof(HostFileTooLarge));

    public static string HostFileBinary => Get(nameof(HostFileBinary));

    public static string HostFileNotUtf8 => Get(nameof(HostFileNotUtf8));

    public static string HostFileMissing => Get(nameof(HostFileMissing));

    public static string HostFileTabName => Get(nameof(HostFileTabName));

    public static string HostFileTabUnsaved => Get(nameof(HostFileTabUnsaved));

    public static string HostFileNeedsSudo => Get(nameof(HostFileNeedsSudo));

    public static string HostFileRevert => Get(nameof(HostFileRevert));

    public static string HostFileReviewSave => Get(nameof(HostFileReviewSave));

    public static string HostFileEdited => Get(nameof(HostFileEdited));

    public static string HostFileSaving => Get(nameof(HostFileSaving));

    public static string HostFileSaved => Get(nameof(HostFileSaved));

    public static string HostFileNotConfirmed => Get(nameof(HostFileNotConfirmed));

    public static string HostFilePlainText => Get(nameof(HostFilePlainText));

    public static string HostFileCaret => Get(nameof(HostFileCaret));

    public static string HostFileStatus => Get(nameof(HostFileStatus));

    public static string HostFileSaveTitle => Get(nameof(HostFileSaveTitle));

    public static string HostFileLinesChanged => Get(nameof(HostFileLinesChanged));

    public static string HostFileLineChanged => Get(nameof(HostFileLineChanged));

    public static string HostFileProductionNote => Get(nameof(HostFileProductionNote));

    public static string HostFileCommandNote => Get(nameof(HostFileCommandNote));

    public static string HostFileSudoNote => Get(nameof(HostFileSudoNote));

    public static string HostFileBackupNote => Get(nameof(HostFileBackupNote));

    public static string HostFileSaveOn => Get(nameof(HostFileSaveOn));

    public static string HostFileBusy => Get(nameof(HostFileBusy));

    public static string HostFileNoTerminal => Get(nameof(HostFileNoTerminal));

    public static string HostFileChangedTitle => Get(nameof(HostFileChangedTitle));

    public static string HostFileChangedMessage => Get(nameof(HostFileChangedMessage));

    public static string HostFileReload => Get(nameof(HostFileReload));

    public static string HostFileSaveAnyway => Get(nameof(HostFileSaveAnyway));

    public static string HostFileSavedNotice => Get(nameof(HostFileSavedNotice));

    public static string HostFileCreatedNotice => Get(nameof(HostFileCreatedNotice));

    public static string HostFileUnsavedTitle => Get(nameof(HostFileUnsavedTitle));

    public static string HostFileUnsavedMessage => Get(nameof(HostFileUnsavedMessage));

    public static string HostFileDiscard => Get(nameof(HostFileDiscard));

    public static string HostFileUnsavedReason => Get(nameof(HostFileUnsavedReason));

    public static string NewHostLabels => Get(nameof(NewHostLabels));

    public static string NewHostLabelsHint => Get(nameof(NewHostLabelsHint));

    public static string NewHostLabelsProblem => Get(nameof(NewHostLabelsProblem));

    public static string NewHostLabelsUsed => Get(nameof(NewHostLabelsUsed));

    public static string NewHostLabelsItem => Get(nameof(NewHostLabelsItem));

    public static string SidebarSwitcherName => Get(nameof(SidebarSwitcherName));

    public static string SidebarSkills => Get(nameof(SidebarSkills));

    public static string SidebarNoSkills => Get(nameof(SidebarNoSkills));

    public static string SidebarWorkspaceSection => Get(nameof(SidebarWorkspaceSection));

    public static string SidebarInstructions => Get(nameof(SidebarInstructions));

    public static string SidebarSettings => Get(nameof(SidebarSettings));

    public static string SidebarNewWorkspace => Get(nameof(SidebarNewWorkspace));

    public static string SidebarOpenWorkspace => Get(nameof(SidebarOpenWorkspace));

    public static string SidebarReveal => Get(nameof(SidebarReveal));

    public static string SidebarHostCount => Get(nameof(SidebarHostCount));

    public static string SidebarHostCountOne => Get(nameof(SidebarHostCountOne));

    public static string SidebarDefaultWorkspace => Get(nameof(SidebarDefaultWorkspace));

    public static string SidebarRevealMac => Get(nameof(SidebarRevealMac));

    public static string SidebarRevealWindows => Get(nameof(SidebarRevealWindows));

    public static string DocumentLogHeading => Get(nameof(DocumentLogHeading));

    public static string DocumentLogShowAll => Get(nameof(DocumentLogShowAll));

    public static string DocumentLogShowFewer => Get(nameof(DocumentLogShowFewer));

    public static string DocumentLogYou => Get(nameof(DocumentLogYou));

    public static string DocumentLogWithAgent => Get(nameof(DocumentLogWithAgent));

    public static string DocumentLogAgentOnly => Get(nameof(DocumentLogAgentOnly));

    public static string DocumentLogNone => Get(nameof(DocumentLogNone));

    public static string DocumentLogLabel => Get(nameof(DocumentLogLabel));

    public static string MemoryIntro => Get(nameof(MemoryIntro));

    public static string MemoryFooter => Get(nameof(MemoryFooter));

    public static string MemoryEverywhere => Get(nameof(MemoryEverywhere));

    public static string MemoryInWorkspace => Get(nameof(MemoryInWorkspace));

    public static string MemoryClear => Get(nameof(MemoryClear));

    public static string MemoryClearSection => Get(nameof(MemoryClearSection));

    public static string MemoryClearTitle => Get(nameof(MemoryClearTitle));

    public static string MemoryClearEverywhere => Get(nameof(MemoryClearEverywhere));

    public static string MemoryEmpty => Get(nameof(MemoryEmpty));

    public static string MemoryFull => Get(nameof(MemoryFull));

    public static string MemoryEditNote => Get(nameof(MemoryEditNote));

    public static string MemoryDeleteNote => Get(nameof(MemoryDeleteNote));

    public static string MemorySave => Get(nameof(MemorySave));

    public static string MemoryReadOnly => Get(nameof(MemoryReadOnly));

    public static string MenuMemory => Get(nameof(MenuMemory));

    public static string SettingsPageMemory => Get(nameof(SettingsPageMemory));

    public static string MemoryClearConfirm => Get(nameof(MemoryClearConfirm));

    public static string MenuRunSkill => Get(nameof(MenuRunSkill));

    public static string MenuSaveSkill => Get(nameof(MenuSaveSkill));

    public static string MenuNoSkills => Get(nameof(MenuNoSkills));

    public static string SkillRunDraft => Get(nameof(SkillRunDraft));

    public static string SkillSaveDraft => Get(nameof(SkillSaveDraft));

    public static string SkillBadge => Get(nameof(SkillBadge));

    public static string SkillRunOn => Get(nameof(SkillRunOn));

    public static string SkillRunOnOne => Get(nameof(SkillRunOnOne));

    public static string SkillRunOnNone => Get(nameof(SkillRunOnNone));

    public static string SkillDescription => Get(nameof(SkillDescription));

    public static string SkillDescriptionHint => Get(nameof(SkillDescriptionHint));

    public static string SkillHowItRollsOut => Get(nameof(SkillHowItRollsOut));

    public static string SkillRunsOn => Get(nameof(SkillRunsOn));

    public static string SkillAddLabel => Get(nameof(SkillAddLabel));

    public static string SkillRemoveLabel => Get(nameof(SkillRemoveLabel));

    public static string SkillMatchesHosts => Get(nameof(SkillMatchesHosts));

    public static string SkillMatchesEvery => Get(nameof(SkillMatchesEvery));

    public static string SkillMatchesNone => Get(nameof(SkillMatchesNone));

    public static string SkillOrder => Get(nameof(SkillOrder));

    public static string SkillOrderOneByOne => Get(nameof(SkillOrderOneByOne));

    public static string SkillOrderBatches => Get(nameof(SkillOrderBatches));

    public static string SkillOrderAllAtOnce => Get(nameof(SkillOrderAllAtOnce));

    public static string SkillBatchSize => Get(nameof(SkillBatchSize));

    public static string SkillMaxHosts => Get(nameof(SkillMaxHosts));

    public static string SkillHealthCheck => Get(nameof(SkillHealthCheck));

    public static string SkillHealthCheckHint => Get(nameof(SkillHealthCheckHint));

    public static string SkillHealthyFor => Get(nameof(SkillHealthyFor));

    public static string SkillHealthyForUnit => Get(nameof(SkillHealthyForUnit));

    public static string SkillGiveUpAfter => Get(nameof(SkillGiveUpAfter));

    public static string SkillGiveUpAfterUnit => Get(nameof(SkillGiveUpAfterUnit));

    public static string SkillOnFailure => Get(nameof(SkillOnFailure));

    public static string SkillOnFailureStop => Get(nameof(SkillOnFailureStop));

    public static string SkillOnFailureContinue => Get(nameof(SkillOnFailureContinue));

    public static string SkillOnFailureAsk => Get(nameof(SkillOnFailureAsk));

    public static string SkillOnFailureStopHint => Get(nameof(SkillOnFailureStopHint));

    public static string SkillOnFailureContinueHint => Get(nameof(SkillOnFailureContinueHint));

    public static string SkillOnFailureAskHint => Get(nameof(SkillOnFailureAskHint));

    public static string SkillNumberProblem => Get(nameof(SkillNumberProblem));

    public static string SkillLabelProblem => Get(nameof(SkillLabelProblem));

    public static string SkillSaveConflict => Get(nameof(SkillSaveConflict));

    public static string SkillRunDraftHosts => Get(nameof(SkillRunDraftHosts));

    public static string And => Get(nameof(And));

    public static string ApprovalHeadingRollout => Get(nameof(ApprovalHeadingRollout));

    public static string ApprovalStartRollout => Get(nameof(ApprovalStartRollout));

    public static string ApprovalRiskRollout => Get(nameof(ApprovalRiskRollout));

    public static string ApprovalRolloutHosts => Get(nameof(ApprovalRolloutHosts));

    public static string ApprovalRolloutHostsEnvironment => Get(nameof(ApprovalRolloutHostsEnvironment));

    public static string RolloutCaption => Get(nameof(RolloutCaption));

    public static string RolloutHeading => Get(nameof(RolloutHeading));

    public static string RolloutOneHost => Get(nameof(RolloutOneHost));

    public static string RolloutHosts => Get(nameof(RolloutHosts));

    public static string RolloutOneByOne => Get(nameof(RolloutOneByOne));

    public static string RolloutInBatches => Get(nameof(RolloutInBatches));

    public static string RolloutAllAtOnce => Get(nameof(RolloutAllAtOnce));

    public static string RolloutHealthCheck => Get(nameof(RolloutHealthCheck));

    public static string RolloutNoHealthCheck => Get(nameof(RolloutNoHealthCheck));

    public static string RolloutPauseAfter => Get(nameof(RolloutPauseAfter));

    public static string RolloutResume => Get(nameof(RolloutResume));

    public static string RolloutStop => Get(nameof(RolloutStop));

    public static string RolloutStopHelp => Get(nameof(RolloutStopHelp));

    public static string RolloutPausedAfter => Get(nameof(RolloutPausedAfter));

    public static string RolloutShowHosts => Get(nameof(RolloutShowHosts));

    public static string RolloutHideHosts => Get(nameof(RolloutHideHosts));

    public static string RolloutHostWaiting => Get(nameof(RolloutHostWaiting));

    public static string RolloutHostNext => Get(nameof(RolloutHostNext));

    public static string RolloutHostWorking => Get(nameof(RolloutHostWorking));

    public static string RolloutHostChecking => Get(nameof(RolloutHostChecking));

    public static string RolloutHostReconnecting => Get(nameof(RolloutHostReconnecting));

    public static string RolloutHostDone => Get(nameof(RolloutHostDone));

    public static string RolloutHostDoneIn => Get(nameof(RolloutHostDoneIn));

    public static string RolloutHostFailed => Get(nameof(RolloutHostFailed));

    public static string RolloutHostSkipped => Get(nameof(RolloutHostSkipped));

    public static string RolloutHealthyOf => Get(nameof(RolloutHealthyOf));

    public static string RolloutHostAccessible => Get(nameof(RolloutHostAccessible));

    public static string RolloutCompleted => Get(nameof(RolloutCompleted));

    public static string RolloutEndedFailed => Get(nameof(RolloutEndedFailed));

    public static string RolloutEndedStopped => Get(nameof(RolloutEndedStopped));

    public static string RolloutProgressText => Get(nameof(RolloutProgressText));

    public static string RolloutMinutes => Get(nameof(RolloutMinutes));

    public static string RolloutSeconds => Get(nameof(RolloutSeconds));

    public static string NoticeChangedOutside => Get(nameof(NoticeChangedOutside));

    public static string NoticeNewSkillOutside => Get(nameof(NoticeNewSkillOutside));

    public static string ReviewChangeAction => Get(nameof(ReviewChangeAction));

    public static string ReviewChangeTitle => Get(nameof(ReviewChangeTitle));

    public static string ReviewChangeMessage => Get(nameof(ReviewChangeMessage));

    public static string ReviewNewSkillTitle => Get(nameof(ReviewNewSkillTitle));

    public static string ReviewNewSkillMessage => Get(nameof(ReviewNewSkillMessage));

    public static string ReviewLooser => Get(nameof(ReviewLooser));

    public static string ReviewKeepMine => Get(nameof(ReviewKeepMine));

    public static string ReviewAcceptChange => Get(nameof(ReviewAcceptChange));

    public static string ReviewLeaveOut => Get(nameof(ReviewLeaveOut));

    public static string ReviewAcceptSkill => Get(nameof(ReviewAcceptSkill));

    public static string NoticeKeptForAgent => Get(nameof(NoticeKeptForAgent));

    public static string NoticeChangeAccepted => Get(nameof(NoticeChangeAccepted));

    public static string NoticeChangeKept => Get(nameof(NoticeChangeKept));

    public static string NoticeConflict => Get(nameof(NoticeConflict));

    public static string ConflictAction => Get(nameof(ConflictAction));

    public static string ConflictTitle => Get(nameof(ConflictTitle));

    public static string ConflictMessage => Get(nameof(ConflictMessage));

    public static string ConflictDetails => Get(nameof(ConflictDetails));

    public static string ConflictKeep => Get(nameof(ConflictKeep));

    public static string ConflictKeepBoth => Get(nameof(ConflictKeepBoth));

    public static string NoticeConflictSettled => Get(nameof(NoticeConflictSettled));

    public static string SidebarRemoveHost => Get(nameof(SidebarRemoveHost));

    public static string RemoveHostTitle => Get(nameof(RemoveHostTitle));

    public static string RemoveHostMessage => Get(nameof(RemoveHostMessage));

    public static string RemoveHostAction => Get(nameof(RemoveHostAction));

    public static string NoticeHostRemoved => Get(nameof(NoticeHostRemoved));

    public static string NoticeWorkspaceReadOnlyNotRemoved => Get(nameof(NoticeWorkspaceReadOnlyNotRemoved));

    public static string AppearanceReduceMotion => Get(nameof(AppearanceReduceMotion));

    public static string AppearanceReduceMotionHint => Get(nameof(AppearanceReduceMotionHint));

    public static string SidebarDefaultWorkspaceDetail => Get(nameof(SidebarDefaultWorkspaceDetail));

    public static string NoTerminalsTitle => Get(nameof(NoTerminalsTitle));

    public static string NoTerminalsMessage => Get(nameof(NoTerminalsMessage));

    public static string NoTerminalsOpen => Get(nameof(NoTerminalsOpen));

    public static string WebAccessHeading => Get(nameof(WebAccessHeading));

    public static string WebSearchQuestion => Get(nameof(WebSearchQuestion));

    public static string WebReadQuestion => Get(nameof(WebReadQuestion));

    public static string WebSearchSent => Get(nameof(WebSearchSent));

    public static string WebReadSent => Get(nameof(WebReadSent));

    public static string WebLeavesNetwork => Get(nameof(WebLeavesNetwork));

    public static string WebDeny => Get(nameof(WebDeny));

    public static string WebDenyDescription => Get(nameof(WebDenyDescription));

    public static string WebAllowOnce => Get(nameof(WebAllowOnce));

    public static string WebAllowOnceSearch => Get(nameof(WebAllowOnceSearch));

    public static string WebAllowOncePage => Get(nameof(WebAllowOncePage));

    public static string WebAllowAlways => Get(nameof(WebAllowAlways));

    public static string WebAllowAlwaysDescription => Get(nameof(WebAllowAlwaysDescription));

    public static string SettingsPageWeb => Get(nameof(SettingsPageWeb));

    public static string WebAlwaysAllow => Get(nameof(WebAlwaysAllow));

    public static string WebAlwaysAllowHint => Get(nameof(WebAlwaysAllowHint));

    public static string WebSearchWith => Get(nameof(WebSearchWith));

    public static string WebServiceProvider => Get(nameof(WebServiceProvider));

    public static string WebServiceBrave => Get(nameof(WebServiceBrave));

    public static string WebServiceTavily => Get(nameof(WebServiceTavily));

    public static string WebServiceSearxng => Get(nameof(WebServiceSearxng));

    public static string WebServiceOff => Get(nameof(WebServiceOff));

    public static string WebKey => Get(nameof(WebKey));

    public static string WebServer => Get(nameof(WebServer));

    public static string WebServerPlaceholder => Get(nameof(WebServerPlaceholder));

    public static string WebServerInvalid => Get(nameof(WebServerInvalid));

    public static string WebHintProvider => Get(nameof(WebHintProvider));

    public static string WebHintBrave => Get(nameof(WebHintBrave));

    public static string WebHintTavily => Get(nameof(WebHintTavily));

    public static string WebHintSearxng => Get(nameof(WebHintSearxng));

    public static string WebHintOff => Get(nameof(WebHintOff));

    public static string WebReadHint => Get(nameof(WebReadHint));

    public static string WebSearched => Get(nameof(WebSearched));

    public static string WebReadPage => Get(nameof(WebReadPage));

    public static string WebNoResults => Get(nameof(WebNoResults));

    public static string ApprovalRiskSendsTerminalText => Get(nameof(ApprovalRiskSendsTerminalText));

    public static string ApprovalRiskAfterWeb => Get(nameof(ApprovalRiskAfterWeb));

    public static string WebComposedAddress => Get(nameof(WebComposedAddress));

    public static string WebComposedAlways => Get(nameof(WebComposedAlways));

    public static string WebHoldsTerminalText => Get(nameof(WebHoldsTerminalText));

    public static string WebInjectionWarning => Get(nameof(WebInjectionWarning));

    public static string ModeIgnorePermissions => Get(nameof(ModeIgnorePermissions));

    public static string ModeIgnorePermissionsHelp => Get(nameof(ModeIgnorePermissionsHelp));

    public static string IgnorePermissionsTitle => Get(nameof(IgnorePermissionsTitle));

    public static string IgnorePermissionsMessage => Get(nameof(IgnorePermissionsMessage));

    public static string IgnorePermissionsConfirm => Get(nameof(IgnorePermissionsConfirm));

    public static string MenuNextArea => Get(nameof(MenuNextArea));

    public static string MenuPreviousArea => Get(nameof(MenuPreviousArea));

    public static string CountNoneOpen => Get(nameof(CountNoneOpen));

    public static string CountOneOpen => Get(nameof(CountOneOpen));

    public static string CountOpen => Get(nameof(CountOpen));

    public static string DocumentsServerFiles => Get(nameof(DocumentsServerFiles));

    public static string MenuMoveArea => Get(nameof(MenuMoveArea));

    public static string MenuAreaLeftOf => Get(nameof(MenuAreaLeftOf));

    public static string MenuAreaRightOf => Get(nameof(MenuAreaRightOf));

    public static string MenuAreaAbove => Get(nameof(MenuAreaAbove));

    public static string MenuAreaBelow => Get(nameof(MenuAreaBelow));

    public static string MenuWindowLeft => Get(nameof(MenuWindowLeft));

    public static string MenuWindowRight => Get(nameof(MenuWindowRight));

    public static string MenuWindowTop => Get(nameof(MenuWindowTop));

    public static string MenuWindowBottom => Get(nameof(MenuWindowBottom));

    public static string MenuResetLayout => Get(nameof(MenuResetLayout));

    public static string NoticeAreaDoesNotFit => Get(nameof(NoticeAreaDoesNotFit));

    public static string AppearanceWindowHeading => Get(nameof(AppearanceWindowHeading));

    public static string AppearanceWindowTheme => Get(nameof(AppearanceWindowTheme));

    public static string WindowThemeSystem => Get(nameof(WindowThemeSystem));

    public static string WindowThemeLight => Get(nameof(WindowThemeLight));

    public static string WindowThemeDark => Get(nameof(WindowThemeDark));

    public static string WindowThemeTerminal => Get(nameof(WindowThemeTerminal));

    public static string AppearanceWindowThemeHint => Get(nameof(AppearanceWindowThemeHint));

    public static string AppearanceTerminalHeading => Get(nameof(AppearanceTerminalHeading));

    public static string AppearanceWindowThemeName => Get(nameof(AppearanceWindowThemeName));

    public static string PlanStepExample => Get(nameof(PlanStepExample));

    public static string PlanStepCommand => Get(nameof(PlanStepCommand));

    public static string PlanStepReadOnly => Get(nameof(PlanStepReadOnly));

    public static string PlanStepChanges => Get(nameof(PlanStepChanges));

    public static string PlanOneComment => Get(nameof(PlanOneComment));

    public static string PlanComments => Get(nameof(PlanComments));

    public static string PlanCommentOnStep => Get(nameof(PlanCommentOnStep));

    public static string PlanStepChanged => Get(nameof(PlanStepChanged));

    public static string PlanStepNew => Get(nameof(PlanStepNew));

    public static string ApprovalLookScopeOne => Get(nameof(ApprovalLookScopeOne));

    public static string ApprovalLookScope => Get(nameof(ApprovalLookScope));

    public static string ApprovalLookWhere => Get(nameof(ApprovalLookWhere));

    public static string ApprovalLookAllow => Get(nameof(ApprovalLookAllow));

    public static string ApprovalLookDecline => Get(nameof(ApprovalLookDecline));

    public static string ApprovalLookOpens => Get(nameof(ApprovalLookOpens));

    public static string PlanCommentSent => Get(nameof(PlanCommentSent));

    public static string PlanDocumentName => Get(nameof(PlanDocumentName));

    public static string PlanDocumentUnsent => Get(nameof(PlanDocumentUnsent));

    public static string PlanDocumentAccessible => Get(nameof(PlanDocumentAccessible));

    public static string PlanNoCommentsToSend => Get(nameof(PlanNoCommentsToSend));

    public static string PlanOneCommentToSend => Get(nameof(PlanOneCommentToSend));

    public static string PlanCommentsToSend => Get(nameof(PlanCommentsToSend));

    public static string PlanStatusWaiting => Get(nameof(PlanStatusWaiting));

    public static string PlanStatusRevised => Get(nameof(PlanStatusRevised));

    public static string PlanStatusApproved => Get(nameof(PlanStatusApproved));

    public static string PlanStatusSentBack => Get(nameof(PlanStatusSentBack));

    public static string PlanStatusDeclined => Get(nameof(PlanStatusDeclined));

    public static string PlanStatusWithdrawn => Get(nameof(PlanStatusWithdrawn));

    public static string PlanFeedbackIntro => Get(nameof(PlanFeedbackIntro));

    public static string PlanRevisedOne => Get(nameof(PlanRevisedOne));

    public static string PlanRevisedMany => Get(nameof(PlanRevisedMany));

    public static string PlanSendComments => Get(nameof(PlanSendComments));

    public static string PlanKeepReviewing => Get(nameof(PlanKeepReviewing));

    public static string PlanApproveAnyway => Get(nameof(PlanApproveAnyway));

    public static string PlanApproveUnsent => Get(nameof(PlanApproveUnsent));

    public static string PlanCommentsTitle => Get(nameof(PlanCommentsTitle));

    public static string PlanCommentsHint => Get(nameof(PlanCommentsHint));

    public static string PlanCommentPlaceholder => Get(nameof(PlanCommentPlaceholder));

    public static string PlanCommentSave => Get(nameof(PlanCommentSave));

    public static string PlanCommentEdit => Get(nameof(PlanCommentEdit));

    public static string PlanCommentDelete => Get(nameof(PlanCommentDelete));

    public static string PlanFindingsTitle => Get(nameof(PlanFindingsTitle));

    public static string PlanStepOn => Get(nameof(PlanStepOn));

    public static string PlanComment => Get(nameof(PlanComment));

    public static string PlanCommentSelectionTip => Get(nameof(PlanCommentSelectionTip));

    public static string PlanCommentQuoteStep => Get(nameof(PlanCommentQuoteStep));

    public static string PlanCommentLabel => Get(nameof(PlanCommentLabel));

    public static string PlanStepsTitle => Get(nameof(PlanStepsTitle));

    public static string LookRunning => Get(nameof(LookRunning));

    public static string LookDone => Get(nameof(LookDone));

    public static string LookDoneOne => Get(nameof(LookDoneOne));

    public static string LookOneHost => Get(nameof(LookOneHost));

    public static string LookHosts => Get(nameof(LookHosts));

    public static string ApprovalReply => Get(nameof(ApprovalReply));

    public static string ApprovalSendReply => Get(nameof(ApprovalSendReply));

    public static string ApprovalApprove => Get(nameof(ApprovalApprove));

    public static string PlanApprove => Get(nameof(PlanApprove));

    public static string PlanRevisedAfterOne => Get(nameof(PlanRevisedAfterOne));

    public static string PlanRevisedAfter => Get(nameof(PlanRevisedAfter));

    public static string PlanCommentSentOne => Get(nameof(PlanCommentSentOne));

    public static string PlanCommentSentMany => Get(nameof(PlanCommentSentMany));

    public static string ApprovalTreatAsReadOnly => Get(nameof(ApprovalTreatAsReadOnly));

    public static string ApprovalTreatAsReadOnlyHint => Get(nameof(ApprovalTreatAsReadOnlyHint));

    public static string SettingsPageCommands => Get(nameof(SettingsPageCommands));

    public static string CommandsLearnedHeading => Get(nameof(CommandsLearnedHeading));

    public static string CommandsNone => Get(nameof(CommandsNone));

    public static string CommandsPlaceholder => Get(nameof(CommandsPlaceholder));

    public static string CommandsAddName => Get(nameof(CommandsAddName));

    public static string CommandsAdd => Get(nameof(CommandsAdd));

    public static string CommandsRemove => Get(nameof(CommandsRemove));

    public static string CommandsRemoveName => Get(nameof(CommandsRemoveName));

    public static string CommandsTeach => Get(nameof(CommandsTeach));

    public static string CommandsTeachName => Get(nameof(CommandsTeachName));

    public static string CommandsHint => Get(nameof(CommandsHint));

    public static string CommandsRecentHeading => Get(nameof(CommandsRecentHeading));

    public static string CommandsRecentHint => Get(nameof(CommandsRecentHint));

    public static string CommandsCantTeach => Get(nameof(CommandsCantTeach));

    public static string CommandsTaught => Get(nameof(CommandsTaught));

    public static string CommandsRemoved => Get(nameof(CommandsRemoved));

    public static string PasswordRemember => Get(nameof(PasswordRemember));

    public static string PasswordSavedRefused => Get(nameof(PasswordSavedRefused));

    public static string PasswordSavedTitle => Get(nameof(PasswordSavedTitle));

    public static string PasswordSavedMessage => Get(nameof(PasswordSavedMessage));

    public static string PasswordSavedTypeIt => Get(nameof(PasswordSavedTypeIt));

    public static string PasswordSavedOnce => Get(nameof(PasswordSavedOnce));

    public static string PasswordSavedAlways => Get(nameof(PasswordSavedAlways));

    public static string NoticeLoginNotSaved => Get(nameof(NoticeLoginNotSaved));

    public static string NoticeLoginNotRead => Get(nameof(NoticeLoginNotRead));

    public static string SettingsPagePasswords => Get(nameof(SettingsPagePasswords));

    public static string PasswordsHeading => Get(nameof(PasswordsHeading));

    public static string PasswordsNone => Get(nameof(PasswordsNone));

    public static string PasswordsAlways => Get(nameof(PasswordsAlways));

    public static string PasswordsAlwaysName => Get(nameof(PasswordsAlwaysName));

    public static string PasswordsForget => Get(nameof(PasswordsForget));

    public static string PasswordsForgetName => Get(nameof(PasswordsForgetName));

    public static string PasswordsHint => Get(nameof(PasswordsHint));

    public static string PasswordsNoStore => Get(nameof(PasswordsNoStore));

    public static string PasswordsForgotten => Get(nameof(PasswordsForgotten));

    public static string PasswordsNotForgotten => Get(nameof(PasswordsNotForgotten));

    public static string ApprovalHeadingPlanMode => Get(nameof(ApprovalHeadingPlanMode));

    public static string ApprovalPlanModeSwitch => Get(nameof(ApprovalPlanModeSwitch));

    public static string ApprovalPlanModeDecline => Get(nameof(ApprovalPlanModeDecline));

    public static string ApprovalRiskPlanMode => Get(nameof(ApprovalRiskPlanMode));

    public static string ApprovalPlanModeAnyHost => Get(nameof(ApprovalPlanModeAnyHost));

    public static string ApprovalPlanModeHosts => Get(nameof(ApprovalPlanModeHosts));

    public static string RailCollapse => Get(nameof(RailCollapse));

    public static string RailExpand => Get(nameof(RailExpand));

    public static string ChatTerminalInputTitle => Get(nameof(ChatTerminalInputTitle));

    public static string ChatTerminalInputText => Get(nameof(ChatTerminalInputText));

    public static string ChatTerminalInputShow => Get(nameof(ChatTerminalInputShow));

    public static string NotifyTerminalInput => Get(nameof(NotifyTerminalInput));

    public static string PlanReadyText => Get(nameof(PlanReadyText));

    public static string PlanReadyHeading => Get(nameof(PlanReadyHeading));

    public static string PlanReadyHeadingNamed => Get(nameof(PlanReadyHeadingNamed));

    public static string PlanShow => Get(nameof(PlanShow));

    public static string StatusLookingAt => Get(nameof(StatusLookingAt));

    public static string LookOpening => Get(nameof(LookOpening));

    public static string LookConnecting => Get(nameof(LookConnecting));

    public static string PlanReject => Get(nameof(PlanReject));

    public static string PlanRejectFeedback => Get(nameof(PlanRejectFeedback));

    public static string PlanRejectComments => Get(nameof(PlanRejectComments));

    public static string PlanAreaName => Get(nameof(PlanAreaName));

    public static string CollapseArea => Get(nameof(CollapseArea));

    public static string ExpandArea => Get(nameof(ExpandArea));

    public static string TerminalConnectionLost => Get(nameof(TerminalConnectionLost));

    public static string TerminalReconnected => Get(nameof(TerminalReconnected));

    public static string TerminalReconnectStopped => Get(nameof(TerminalReconnectStopped));

    public static string TerminalReconnecting => Get(nameof(TerminalReconnecting));

    public static string PlanStatusStepAdded => Get(nameof(PlanStatusStepAdded));

    public static string PlanStepAddedText => Get(nameof(PlanStepAddedText));

    public static string PlanStepAdded => Get(nameof(PlanStepAdded));

    public static string ProviderModelOther => Get(nameof(ProviderModelOther));

    public static string ProviderModelName => Get(nameof(ProviderModelName));

    public static string ProviderModelNameHint => Get(nameof(ProviderModelNameHint));

    public static string ProviderKnownSource => Get(nameof(ProviderKnownSource));

    public static string ProviderTokens => Get(nameof(ProviderTokens));

    public static string ProviderLongPromptPrice => Get(nameof(ProviderLongPromptPrice));

    public static string ProviderNoPrice => Get(nameof(ProviderNoPrice));

    public static string ContextUsedDetail => Get(nameof(ContextUsedDetail));

    public static string TokensThousands => Get(nameof(TokensThousands));

    public static string TokensMillions => Get(nameof(TokensMillions));

    public static string Format(string format, params object?[] arguments) => string.Format(CultureInfo.CurrentCulture, format, arguments);

    private static string Get(string name) => s_resources.GetString(name, CultureInfo.CurrentUICulture) ?? name;
}
