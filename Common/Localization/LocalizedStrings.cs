namespace Common.Localization;

public abstract class LocalizedStrings
{
    public abstract string Code { get; }
    public abstract string DisplayName { get; }
    public abstract string EnglishName { get; }

    public abstract string ButtonCancel { get; }
    public abstract string ButtonSettings { get; }
    public abstract string ButtonStart { get; }

    public abstract string DialogSelectGameFile { get; }

    public abstract string ErrorNoMetadata { get; }

    public abstract string FilterAllFiles { get; }
    public abstract string FilterSwitchFiles { get; }

    public abstract string HintSelectOutput { get; }

    public abstract string HeaderFileName { get; }
    public abstract string HeaderFilePath { get; }
    public abstract string HeaderTitle { get; }
    public abstract string HeaderPatch { get; }
    public abstract string HeaderVersion { get; }
    public abstract string HeaderInput { get; }
    public abstract string HeaderOutput { get; }

    public abstract string LabelFile { get; }
    public abstract string LabelCompression { get; }
    public abstract string HeaderCompressionRatio { get; }
    public abstract string LabelOutputFormat { get; }    

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

    public abstract string MainErrNoFiles { get; }
    public abstract string MainErrNoKeys { get; }
    public abstract string MainErrNoOutput { get; }
    public abstract string MainLogAllComplete(object arg0);
    public abstract string MainLogRecalculating { get; }
    public abstract string MainMsgDone { get; }
    public abstract string MainMsgSplitDone { get; }

    public abstract string MenuOpenFolder { get; }

    public abstract string MsgTitleError { get; }
    public abstract string MsgTitleInfo { get; }
    public abstract string MsgTitleQuestion { get; }
    public abstract string MsgTitleWarning { get; }

    public abstract string StatusAnalyzing { get; }
    public abstract string StatusNoKey { get; }

    public abstract string ToolTipAddFile { get; }
    public abstract string ToolTipAddFolder { get; }
    public abstract string ToolTipBlockMode { get; }
    public abstract string ToolTipBlocklessMode { get; }
    public abstract string ToolTipOpenWorkSpace { get; }
    public abstract string ToolTipClear { get; }
    public abstract string ToolTipRemoveSelected { get; }
    public abstract string ToolTipValidateCompress { get; }
    public abstract string ButtonAddFile { get; }
    public abstract string ButtonAddFolder { get; }
    public abstract string ButtonRemoveSelected { get; }
    public abstract string ButtonClear { get; }
    public abstract string MenuRemoveSelected { get; }
    public abstract string MenuClear { get; }
    public abstract string TabPatch { get; }
    public abstract string LabelPatch { get; }
    public abstract string TabSettings { get; }
    public abstract string LabelSettings { get; }
    public abstract string LabelVersion { get; }
    public abstract string HintDropCueChd { get; }
    public abstract string HintDropSourceNsp { get; }
    public abstract string HintDragOrSelectWorkFolder { get; }
    public abstract string HintDragOrSelectOutputFolder { get; }
    public abstract string HintDropImageFile { get; }
    public abstract string HintDropFileOrFolder { get; }
    public abstract string HintDropGameFileOrFolder { get; }
    public abstract string HintRepackReplaceRule { get; }
    public abstract string HintSplitExpandRule { get; }
    public abstract string LabelGameName { get; }
    public abstract string LabelPublisher { get; }
    public abstract string LabelGameId { get; }
    public abstract string LabelNspTitleId { get; }
    public abstract string LabelWideScreen { get; }
    public abstract string LabelSoundVolume { get; }
    public abstract string LabelLz4Compression { get; }
    public abstract string LabelSupportedLanguages { get; }
    public abstract string LabelMultiRomIndex { get; }
    public abstract string LabelMergeMode { get; }
    public abstract string LabelEstimated { get; }
    public abstract string LabelActual { get; }
    public abstract string LabelFileCount(object arg0);
    public abstract string HeaderFileNameCompare { get; }
    public abstract string DescConvertFormats { get; }
    public abstract string DescRenameByFormat { get; }
    public abstract string ButtonForceLanguage { get; }
    public abstract string ButtonCancelForceLanguage { get; }
    public abstract string ButtonUnpack { get; }
    public abstract string ButtonRepackFolderBased { get; }
    public abstract string ButtonUnpackAndRepackStreaming { get; }
    public abstract string TabRepack { get; }
    public abstract string TabMerge { get; }
    public abstract string TabSplit { get; }
    public abstract string TabLz4Compression { get; }
    public abstract string TabFormatConvert { get; }
    public abstract string TabSaturnNspConvert { get; }
    public abstract string TabIgnoreVersion { get; }
    public abstract string TabRename { get; }
    public abstract string RadioMergeGroupByTitleId { get; }
    public abstract string RadioMergeAll { get; }
    public abstract string MenuSelectFolder { get; }
    public abstract string MenuSelectArchive { get; }
    public abstract string MenuDeselect { get; }
    public abstract string MenuFindInFolder { get; }
    public abstract string MenuFindInArchive { get; }
    public abstract string ToolTipTitleIdInput { get; }
    public abstract string ToolTipForceLanguage { get; }
    public abstract string ToolTipMergeGroupByTitleId { get; }
    public abstract string ToolTipMergeAll { get; }
    public abstract string ToolTipRomFsLz4 { get; }
    public abstract string ToolTipBatchKoreanPatch { get; }
    public abstract string ToolTipSelectAllToggle { get; }
    public abstract string ToolTipExpandCollapse { get; }
    public abstract string HeaderSize { get; }
    public abstract string ToolTipBrowseOutput { get; }
    public abstract string HeaderConvert { get; }
    public abstract string TabGeneral { get; }
    public abstract string TabArcade { get; }
    public abstract string TabConvert { get; }
    public abstract string TabUtil { get; }
    public abstract string ButtonReset { get; }
    public abstract string ButtonCalculateHash { get; }
    public abstract string ButtonOk { get; }
    public abstract string ToggleChangeOutputFolder { get; }
    public abstract string ToggleAutoCompressAfterPatch { get; }
    public abstract string ToggleAutoVersionDateInName { get; }
    public abstract string MenuSelectFileIpsZip { get; }
    public abstract string MenuClearLog { get; }
    public abstract string MenuCopyLog { get; }
    public abstract string ToolTipSelectFile { get; }
    public abstract string ToolTipRestoreDefaultPath { get; }
    public abstract string ToolTipChangePatchPackage { get; }
    public abstract string ToolTipChangePatchFile { get; }
    public abstract string ToolTipAddSequentialPatch { get; }
    public abstract string ToolTipOpenGithubReleases { get; }
    public abstract string ToolTipOpenHelp { get; }
    public abstract string LabelSource { get; }
    public abstract string LabelSelectPatch { get; }
    public abstract string LabelOthers { get; }
    public abstract string LabelExpectedFileName { get; }
    public abstract string LabelSupportedPatchFormats { get; }
    public abstract string LabelSourceAutoExtractFormats { get; }
    public abstract string LabelFormatListIsoStandard { get; }
    public abstract string LabelFormatList3dsCciCia { get; }
    public abstract string LabelFormat3dsCci { get; }
    public abstract string LabelFormatChdIsoBased { get; }
    public abstract string LabelFormatBinCueSingleTrack { get; }
    public abstract string DescAutoCompressByRule { get; }
    public abstract string DescConvertByRule { get; }
    public abstract string DescAutoMatchByExtension { get; }
    public abstract string DescAutoVersionDateInName { get; }
    public abstract string DescPc98Hdi { get; }
    public abstract string DescArchivePasswordProtected { get; }
    public abstract string HintDropPatchFile { get; }
    public abstract string HintDropArcadeFiles { get; }
    public abstract string StatusMatched { get; }
    public abstract string StatusUnmatched { get; }
    public abstract string StatusHashCancelledByPatch { get; }
    public abstract string StatusHashFailed { get; }
    public abstract string StatusWrongPassword { get; }
    public abstract string TitleArchivePassword { get; }
}