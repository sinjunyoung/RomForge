namespace Common.Localization;

public abstract class LocalizedStrings
{
    // Language
    public abstract string Code { get; }
    public abstract string DisplayName { get; }
    public abstract string EnglishName { get; }

    // Button
    public abstract string ButtonAddFile { get; }
    public abstract string ButtonAddFolder { get; }
    public abstract string ButtonAnalyze { get; }
    public abstract string ButtonCalculateHash { get; }
    public abstract string ButtonCancel { get; }
    public abstract string ButtonCancelForceLanguage { get; }
    public abstract string ButtonChange { get; }
    public abstract string ButtonClear { get; }
    public abstract string ButtonDownload { get; }
    public abstract string ButtonFilterAll { get; }
    public abstract string ButtonFilterGame { get; }
    public abstract string ButtonFilterUpdate { get; }
    public abstract string ButtonForceLanguage { get; }
    public abstract string ButtonInstall { get; }
    public abstract string ButtonLater { get; }
    public abstract string ButtonLoadGameList { get; }
    public abstract string ButtonOk { get; }
    public abstract string ButtonRecoverTitleDb { get; }
    public abstract string ButtonRemoveSelected { get; }
    public abstract string ButtonRepackFolderBased { get; }
    public abstract string ButtonReset { get; }
    public abstract string ButtonSelectFile { get; }
    public abstract string ButtonSettings { get; }
    public abstract string ButtonStart { get; }
    public abstract string ButtonUnpack { get; }
    public abstract string ButtonUnpackAndRepackFull { get; }
    public abstract string ButtonUnpackAndRepackStreaming { get; }

    // Check
    public abstract string CheckExtraOption { get; }
    public abstract string CheckVideoFreezeFix { get; }

    // Description
    public abstract string DescArchivePasswordProtected { get; }
    public abstract string DescAutoCompressByRule { get; }
    public abstract string DescAutoMatchByExtension { get; }
    public abstract string DescAutoVersionDateInName { get; }
    public abstract string DescCalculateFileHash { get; }
    public abstract string DescCertsBinUsedForCia { get; }
    public abstract string DescClickOrDropToChange { get; }
    public abstract string DescConvertByRule { get; }
    public abstract string DescConvertByRuleMutual { get; }
    public abstract string DescConvertFormats { get; }
    public abstract string DescCreateSingleTrackCue { get; }
    public abstract string DescDecryptFiles { get; }
    public abstract string DescExtractCertsBin { get; }
    public abstract string DescInstallToConsole { get; }
    public abstract string DescMp3WavAutoConvertAt3 { get; }
    public abstract string DescOutputFolderDefault { get; }
    public abstract string DescPackMax5 { get; }
    public abstract string DescPc98Hdi { get; }
    public abstract string DescRenameByFormat { get; }
    public abstract string DescRestorePbpToBinCue { get; }
    public abstract string DescSelectSdLoadList { get; }
    public abstract string DescTistoryEnterUrl { get; }
    public abstract string DescTistoryFindAttachments { get; }
    public abstract string DescVitaAutoClassify { get; }
    public abstract string DescVitaGroupToNoNpDrm { get; }
    public abstract string DescVitaKeyRequiredShared { get; }
    public abstract string DescVitaPatchMerge { get; }
    public abstract string DescVitaPkgKeyRequired { get; }
    public abstract string DescWiiUAutoClassify { get; }

    // Dialog
    public abstract string DialogSelectGameFile { get; }

    // Error
    public abstract string ErrorNoMetadata { get; }

    // Filter
    public abstract string FilterAllFiles { get; }
    public abstract string FilterSwitchFiles { get; }

    // Header
    public abstract string HeaderCompressionRatio { get; }
    public abstract string HeaderConvert { get; }
    public abstract string HeaderFileName { get; }
    public abstract string HeaderFileNameCompare { get; }
    public abstract string HeaderFilePath { get; }
    public abstract string HeaderForcedLanguage { get; }
    public abstract string HeaderFormat { get; }
    public abstract string HeaderInput { get; }
    public abstract string HeaderKey { get; }
    public abstract string HeaderLocalTitle { get; }
    public abstract string HeaderOutput { get; }
    public abstract string HeaderPatch { get; }
    public abstract string HeaderProductCode { get; }
    public abstract string HeaderResult { get; }
    public abstract string HeaderSize { get; }
    public abstract string HeaderTitle { get; }
    public abstract string HeaderVersion { get; }

    // Hint
    public abstract string HintDragOrClickPatch3ds { get; }
    public abstract string HintDragOrClickRiivolutionPatch { get; }
    public abstract string HintDragOrClickRom3ds { get; }
    public abstract string HintDragOrClickWiiDisc { get; }
    public abstract string HintDragOrSelectOutputFolder { get; }
    public abstract string HintDragOrSelectWorkFolder { get; }
    public abstract string HintDropArcadeFiles { get; }
    public abstract string HintDropCueChd { get; }
    public abstract string HintDropFile { get; }
    public abstract string HintDropFileOrFolder { get; }
    public abstract string HintDropGameFileOrFolder { get; }
    public abstract string HintDropImageFile { get; }
    public abstract string HintDropOrClickAudio { get; }
    public abstract string HintDropPatchFile { get; }
    public abstract string HintDropSourceNsp { get; }
    public abstract string HintDropVitaFiles { get; }
    public abstract string HintDropVitaPkg { get; }
    public abstract string HintDropWiiUFiles { get; }
    public abstract string HintRepackReplaceRule { get; }
    public abstract string HintSearchGame { get; }
    public abstract string HintSelectOutput { get; }
    public abstract string HintSoundCancel { get; }
    public abstract string HintSoundComplete { get; }
    public abstract string HintSoundError { get; }
    public abstract string HintSplitExpandRule { get; }

    // Label
    public abstract string LabelActual { get; }
    public abstract string LabelAlgorithm { get; }
    public abstract string LabelAllFormats { get; }
    public abstract string LabelBackgroundMusic { get; }
    public abstract string LabelBootLogo { get; }
    public abstract string LabelCompression { get; }
    public abstract string LabelCompressionAlgorithmIso { get; }
    public abstract string LabelCompressionLevel { get; }
    public abstract string LabelCompressionLevelNszXcz { get; }
    public abstract string LabelCompressionMode { get; }
    public abstract string LabelDefaultImage { get; }
    public abstract string LabelDownloadLocation { get; }
    public abstract string LabelEstimated { get; }
    public abstract string LabelExpectedFileName { get; }
    public abstract string LabelFile { get; }
    public abstract string LabelFileCount(object arg0);
    public abstract string LabelFormat3dsCci { get; }
    public abstract string LabelFormatBinCueSingleTrack { get; }
    public abstract string LabelFormatChdIsoBased { get; }
    public abstract string LabelFormatList3dsCciCia { get; }
    public abstract string LabelFormatListIsoStandard { get; }
    public abstract string LabelFormatListLoadiineFolderWua { get; }
    public abstract string LabelFormatListWuaWupLoadiine { get; }
    public abstract string LabelFormatListWupFolderWua { get; }
    public abstract string LabelFormatListWupLoadiine { get; }
    public abstract string LabelGameId { get; }
    public abstract string LabelGameName { get; }
    public abstract string LabelGeneral { get; }
    public abstract string LabelLanguage { get; }
    public abstract string LabelLz4Compression { get; }
    public abstract string LabelMergeMode { get; }
    public abstract string LabelMultiRomIndex { get; }
    public abstract string LabelNoteCemuArchive { get; }
    public abstract string LabelNoteDiscImage { get; }
    public abstract string LabelNoteFolder { get; }
    public abstract string LabelNspTitleId { get; }
    public abstract string LabelOptions { get; }
    public abstract string LabelOthers { get; }
    public abstract string LabelOutputFormat { get; }
    public abstract string LabelOutputOptions { get; }
    public abstract string LabelPatch { get; }
    public abstract string LabelPatchInfo { get; }
    public abstract string LabelPbpPacking { get; }
    public abstract string LabelPkgCount(object arg0);
    public abstract string LabelPopsEmulatorSettings { get; }
    public abstract string LabelPublisher { get; }
    public abstract string LabelQuality { get; }
    public abstract string LabelSelectPatch { get; }
    public abstract string LabelSettings { get; }
    public abstract string LabelSoundVolume { get; }
    public abstract string LabelSource { get; }
    public abstract string LabelSourceAutoExtractFormats { get; }
    public abstract string LabelSupportedLanguages { get; }
    public abstract string LabelSupportedPatchFormats { get; }
    public abstract string LabelTistoryUrl { get; }
    public abstract string LabelVersion { get; }
    public abstract string LabelWideScreen { get; }

    // Log
    public abstract string LogAnalyzeMetadata { get; }
    public abstract string LogCompressAndMerge { get; }
    public abstract string LogCompressAndSplit { get; }
    public abstract string LogCompressionRatio { get; }
    public abstract string LogDecompressAndMerge { get; }
    public abstract string LogDeleteIncompleteFile { get; }
    public abstract string LogError(object arg0);
    public abstract string LogFinalId(object arg0, object arg1);
    public abstract string LogMergeComplete(object arg0);
    public abstract string LogMergeFailed(object? arg0, object arg1);
    public abstract string LogMerging { get; }
    public abstract string LogModeCompress { get; }
    public abstract string LogModeDecompress { get; }
    public abstract string LogProcessError { get; }
    public abstract string LogProcessStart { get; }
    public abstract string LogSplitComplete(object arg0);
    public abstract string LogSplitFailed(object arg0, object arg1);
    public abstract string LogSplitPreparing(object arg0);
    public abstract string LogSplitting { get; }
    public abstract string LogStatusCompressing { get; }
    public abstract string LogStatusCopying { get; }
    public abstract string LogStatusDecompressing { get; }
    public abstract string LogStatusDone { get; }
    public abstract string LogStatusValidating { get; }
    public abstract string LogTitleGroupDetected(object arg0);
    public abstract string LogValidationComplete { get; }
    public abstract string LogValidationStart { get; }

    // Main
    public abstract string MainErrNoFiles { get; }
    public abstract string MainErrNoKeys { get; }
    public abstract string MainErrNoOutput { get; }
    public abstract string MainLogAllComplete(object arg0);
    public abstract string MainLogRecalculating { get; }
    public abstract string MainMsgDone { get; }
    public abstract string MainMsgSplitDone { get; }

    // Menu
    public abstract string MenuClear { get; }
    public abstract string MenuClearLog { get; }
    public abstract string MenuCopyLog { get; }
    public abstract string MenuDeselect { get; }
    public abstract string MenuExtractCci { get; }
    public abstract string MenuExtractCia { get; }
    public abstract string MenuFindInArchive { get; }
    public abstract string MenuFindInFolder { get; }
    public abstract string MenuOpenFolder { get; }
    public abstract string MenuRemoveSelected { get; }
    public abstract string MenuSelectArchive { get; }
    public abstract string MenuSelectFileIpsZip { get; }
    public abstract string MenuSelectFileZip7z { get; }
    public abstract string MenuSelectFileZip7zXml { get; }
    public abstract string MenuSelectFolder { get; }

    // Message
    public abstract string MsgTitleError { get; }
    public abstract string MsgTitleInfo { get; }
    public abstract string MsgTitleQuestion { get; }
    public abstract string MsgTitleWarning { get; }

    // Option
    public abstract string OptionLevel0None { get; }
    public abstract string OptionLevel18Recommended { get; }
    public abstract string OptionLevel3Fastest { get; }
    public abstract string OptionLevel5Balanced { get; }
    public abstract string OptionLevel9Balanced { get; }
    public abstract string OptionLevel9Max { get; }

    // Radio
    public abstract string RadioBlock { get; }
    public abstract string RadioBlockless { get; }
    public abstract string RadioCciEmulator { get; }
    public abstract string RadioCiaConsole { get; }
    public abstract string RadioConsoleRepatch { get; }
    public abstract string RadioEmulatorAppAddcont { get; }
    public abstract string RadioFolderPerTitleId { get; }
    public abstract string RadioLowercase { get; }
    public abstract string RadioMergeAll { get; }
    public abstract string RadioMergeGroupByTitleId { get; }
    public abstract string RadioOutputGameId { get; }
    public abstract string RadioOutputNormal(object arg0);
    public abstract string RadioQuality30s { get; }
    public abstract string RadioQuality60s { get; }
    public abstract string RadioRvzDolphin { get; }
    public abstract string RadioUppercase { get; }
    public abstract string RadioWbfsUsbLoader { get; }
    public abstract string RadioWuaEmulator { get; }
    public abstract string RadioWupConsole { get; }
    public abstract string RadioZcciAzahar { get; }
    public abstract string RadioZipPerTitleId { get; }

    // Status
    public abstract string StatusAnalyzing { get; }
    public abstract string StatusConvertingToAt3 { get; }
    public abstract string StatusDownloadingImage { get; }
    public abstract string StatusHashCancelledByPatch { get; }
    public abstract string StatusHashFailed { get; }
    public abstract string StatusLoading { get; }
    public abstract string StatusMatched { get; }
    public abstract string StatusNoKey { get; }
    public abstract string StatusPresetLoaded { get; }
    public abstract string StatusUnmatched { get; }
    public abstract string StatusWrongPassword { get; }

    // Tab
    public abstract string TabArcade { get; }
    public abstract string TabCalculateHash { get; }
    public abstract string TabConvert { get; }
    public abstract string TabCreateCue { get; }
    public abstract string TabDecrypt { get; }
    public abstract string TabExtractCertsBin { get; }
    public abstract string TabFormatConvert { get; }
    public abstract string TabGeneral { get; }
    public abstract string TabIgnoreVersion { get; }
    public abstract string TabInstall { get; }
    public abstract string TabInstalledGames { get; }
    public abstract string TabInstallNewGame { get; }
    public abstract string TabKeyFile { get; }
    public abstract string TabLz4Compression { get; }
    public abstract string TabMerge { get; }
    public abstract string TabPatch { get; }
    public abstract string TabPbpPacking { get; }
    public abstract string TabPbpUnpack { get; }
    public abstract string TabRename { get; }
    public abstract string TabRepack { get; }
    public abstract string TabSaturnNspConvert { get; }
    public abstract string TabSettings { get; }
    public abstract string TabSplit { get; }
    public abstract string TabTistoryDownloader { get; }
    public abstract string TabUtil { get; }

    // Title
    public abstract string TitleArchivePassword { get; }

    // Toggle
    public abstract string ToggleAlwaysOnTop { get; }
    public abstract string ToggleAutoCompressAfterPatch { get; }
    public abstract string ToggleAutoExtractDelete { get; }
    public abstract string ToggleAutoVersionDateInName { get; }
    public abstract string ToggleChangeOutputFolder { get; }
    public abstract string ToggleCheckUpdateOnStart { get; }
    public abstract string ToggleMergePatchToGame { get; }
    public abstract string ToggleOtherFormats { get; }
    public abstract string ToggleSoundEffects { get; }

    // ToolTip
    public abstract string ToolTipAddFile { get; }
    public abstract string ToolTipAddFolder { get; }
    public abstract string ToolTipAddSequentialPatch { get; }
    public abstract string ToolTipAutoExtractDelete { get; }
    public abstract string ToolTipBatchKoreanPatch { get; }
    public abstract string ToolTipBlocklessMode { get; }
    public abstract string ToolTipBlockMode { get; }
    public abstract string ToolTipBrowseOutput { get; }
    public abstract string ToolTipChangePatchFile { get; }
    public abstract string ToolTipChangePatchPackage { get; }
    public abstract string ToolTipClear { get; }
    public abstract string ToolTipCopyHash { get; }
    public abstract string ToolTipDragFolderToChangeDownload { get; }
    public abstract string ToolTipExpandCollapse { get; }
    public abstract string ToolTipForcedLanguage { get; }
    public abstract string ToolTipForceLanguage { get; }
    public abstract string ToolTipGameNameMax63 { get; }
    public abstract string ToolTipLicenseKey { get; }
    public abstract string ToolTipLicenseKeyPatchFallback { get; }
    public abstract string ToolTipMergeAll { get; }
    public abstract string ToolTipMergeGroupByTitleId { get; }
    public abstract string ToolTipMovableSedGuide { get; }
    public abstract string ToolTipOpenGithubReleases { get; }
    public abstract string ToolTipOpenHelp { get; }
    public abstract string ToolTipOpenWorkSpace { get; }
    public abstract string ToolTipPlayPause { get; }
    public abstract string ToolTipPublisherMax63 { get; }
    public abstract string ToolTipRecoverTitleDb { get; }
    public abstract string ToolTipRemoveSelected { get; }
    public abstract string ToolTipRestoreDefaultPath { get; }
    public abstract string ToolTipRomFsLz4 { get; }
    public abstract string ToolTipSelectAllToggle { get; }
    public abstract string ToolTipSelectFile { get; }
    public abstract string ToolTipTistoryUrlInput { get; }
    public abstract string ToolTipTitleIdInput { get; }
    public abstract string ToolTipValidateCompress { get; }

    // Update
    public abstract string LatestVersion { get; }
    public abstract string NoUpdateNotes { get; }
    public abstract string UpdateAvailable { get; }
    public abstract string UpdateDescription { get; }
    public abstract string UpdateTitle { get; }

    public abstract string StatusWaiting { get; }
    public abstract string StatusConverting { get; }
    public abstract string StatusDecrypting { get; }
    public abstract string StatusExtracting { get; }
    public abstract string StatusDownloading { get; }
    public abstract string StatusUnzipping { get; }
    public abstract string StatusUnzipDone { get; }
    public abstract string StatusUnzipFailed { get; }
    public abstract string StatusDone { get; }
    public abstract string StatusFailed { get; }
    public abstract string StatusCancelled { get; }
    public abstract string StatusUnsupported { get; }
    public abstract string StatusSkipped { get; }
    public abstract string RenameStatusReady { get; }
    public abstract string RenameStatusUnchanged { get; }
    public abstract string RenameStatusMissingInfo { get; }
    public abstract string RenameStatusEmptyName { get; }
    public abstract string RenameStatusNameTooLong { get; }
    public abstract string RenameStatusDuplicate { get; }
    public abstract string RenameStatusTargetExists { get; }

    public string GetItemStatus(ItemStatus status) => status switch
    {
        ItemStatus.Waiting => StatusWaiting,
        ItemStatus.Converting => StatusConverting,
        ItemStatus.Decrypting => StatusDecrypting,
        ItemStatus.Extracting => StatusExtracting,
        ItemStatus.Downloading => StatusDownloading,
        ItemStatus.Unzipping => StatusUnzipping,
        ItemStatus.UnzipDone => StatusUnzipDone,
        ItemStatus.UnzipFailed => StatusUnzipFailed,
        ItemStatus.Done => StatusDone,
        ItemStatus.Failed => StatusFailed,
        ItemStatus.Cancelled => StatusCancelled,
        ItemStatus.Unsupported => StatusUnsupported,
        ItemStatus.Skipped => StatusSkipped,
        _ => string.Empty
    };

    public abstract string ProgressElapsed(object arg0);
    public abstract string ProgressCompletedCount(object arg0, object arg1);
    public abstract string ProgressHashingPercent(object arg0);
    public abstract string ProgressAnalyzingPatchInfo { get; }
    public abstract string ProgressExtractingPatch { get; }
    public abstract string ProgressUnpackingFile(object arg0);
    public abstract string ProgressRepackingFile(object arg0);
    public abstract string ProgressApplyingPatchTo(object arg0);
    public abstract string ProgressHashingPartitions(object arg0);
    public abstract string ProgressCompressingWbfs(object arg0);
    public abstract string ProgressCompressingRvz(object arg0);
    public abstract string ProgressCreatingIso(object arg0);
    public abstract string ProgressWupCreated(object arg0, object arg1);
    public abstract string MsgCancelled { get; }
    public abstract string StatusInitializing { get; }
    public abstract string StatusSelectSdAndMovable { get; }
    public abstract string ProgressLoadingCount(object arg0, object arg1);
    public abstract string ProgressScanningCount(object arg0, object arg1);
    public abstract string StatusRecoveringTitleDb { get; }
    public abstract string StatusRecoveryDone(object arg0);
    public abstract string StatusExtractingTitle(object? arg0, object arg1);
    public abstract string StatusInstallStarted(object? arg0);
    public abstract string StatusInstallDone(object? arg0);
    public abstract string StatusScanningTitles { get; }
    public abstract string StatusLoadingMetadata(object arg0);
    public abstract string StatusTitlesLoaded(object arg0);
    public abstract string ProgressPreparing { get; }
    public abstract string ProgressHashing { get; }
    public abstract string ProgressCompressingNsz { get; }

    public abstract string StatusUnpacking { get; }
    public abstract string StatusApplyingPatch { get; }
    public abstract string StatusApplyingPatchAndCompressing { get; }
    public abstract string StatusLoadingSource { get; }
    public abstract string StatusInstalling { get; }
    public abstract string StatusExtractingCia { get; }
    public abstract string ProgressHashingNca { get; }
    public abstract string ProgressCompressingRomfs { get; }
    public abstract string ProgressBuildingPfs0 { get; }
    public abstract string ProgressBuildingRomfs { get; }
    public abstract string ProgressWritingRomfs { get; }
    public abstract string ProgressCreatingIvfc(object arg0, object arg1);
    public abstract string ProgressFinalizingNsp { get; }
    public abstract string ProgressBuildingProgram { get; }
    public abstract string ProgressApplyingExefsPatches(object arg0);
    public abstract string ProgressApplyingLabel(object arg0);
    public abstract string LabelDlcPatch(object arg0);
    public abstract string LabelKoreanPatch { get; }
    public abstract string ProgressUnpackingNcaEntry(object arg0, object arg1);
    public abstract string ProgressExtractDone { get; }
    public abstract string ProgressCompressDone { get; }
    public abstract string ProgressConvertingBinCue { get; }
    public abstract string ProgressConvertingIso { get; }
    public abstract string ProgressPacking { get; }
    public abstract string ProgressScanningTitle(object arg0);
    public abstract string ProgressScanError(object arg0, object arg1);
    public abstract string ProgressExtractingIso { get; }
    public abstract string ProgressCopyingTrack(object arg0, object arg1);
    public abstract string ProgressConvertingChd { get; }
    public abstract string ProgressCompressingZcci { get; }
    public abstract string ProgressConvertingFormat { get; }
    public abstract string ProgressPatchDone { get; }
    public abstract string ProgressExtractingSource { get; }
    public abstract string ProgressGdParsingMetadata { get; }
    public abstract string ProgressGdPatchAllDone { get; }
    public abstract string ProgressGdExtractingDcp(object arg0);
    public abstract string ProgressGdApplyingXdelta(object arg0);
    public abstract string ProgressGdPreparingMedia { get; }
    public abstract string ProgressApplyingPatchCount(object arg0, object arg1);
    public abstract string ProgressGdRelocatingIso9660(object arg0, object arg1);
    public abstract string ProgressGdMergingSectors { get; }
    public abstract string ProgressGdCreatingTrack(object arg0, object arg1);
    public abstract string ProgressGdAnalyzingTracks { get; }
    public abstract string ProgressGdCopyingTrack(object arg0, object arg1, object arg2);
    public abstract string ProgressGdUpdatingIndex { get; }
    public abstract string ProgressGdRebuildAllDone { get; }
    public abstract string ProgressGdAligningSectors { get; }
    public abstract string ProgressGdWritingRawSectors { get; }
    public abstract string ProgressGdTrackWritten { get; }
}