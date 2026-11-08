namespace Common.Localization;

public abstract class LocalizedStrings
{
    public abstract string Code { get; }
    public abstract string DisplayName { get; }

    public abstract string ButtonCancel { get; }
    public abstract string ButtonSettings { get; }
    public abstract string ButtonStart { get; }
    public abstract string DialogSelectGameFile { get; }
    public abstract string ErrorNoCompressFiles { get; }
    public abstract string ErrorNoDecompressFiles { get; }
    public abstract string ErrorNoMetadata { get; }
    public abstract string FilterAllFiles { get; }
    public abstract string FilterSwitchFiles { get; }
    public abstract string HintDragAndDrop { get; }
    public abstract string HintSelectOutput { get; }
    public abstract string HeaderExt { get; }
    public abstract string HeaderFileName { get; }
    public abstract string HeaderFilePath { get; }
    public abstract string HeaderSize { get; }
    public abstract string HeaderType { get; }
    public abstract string LabelCompression { get; }
    public abstract string LabelOutputPath { get; }
    public abstract string LogAnalyzeMetadata { get; }
    public abstract string LogBaseMissingSkip(object arg0);
    public abstract string LogCompressAndMerge { get; }
    public abstract string LogCompressAndSplit { get; }
    public abstract string LogCompressionRatio { get; }
    public abstract string LogDecompressAndMerge { get; }
    public abstract string LogDecompressAndSplit { get; }
    public abstract string LogDeleteIncompleteFile { get; }
    public abstract string LogError(object arg0);
    public abstract string LogFinalId(object arg0, object arg1);
    public abstract string LogMergeComplete(object arg0);
    public abstract string LogMergeFailed(object? arg0, object arg1);
    public abstract string LogMergeStart { get; }
    public abstract string LogMerging { get; }
    public abstract string LogModeCompress { get; }
    public abstract string LogModeDecompress { get; }
    public abstract string LogProcessError { get; }
    public abstract string LogProcessStart { get; }
    public abstract string LogSplitComplete(object arg0);
    public abstract string LogSplitDecompressing(object arg0);
    public abstract string LogSplitExtracting(object arg0);
    public abstract string LogSplitFailed(object arg0, object arg1);
    public abstract string LogSplitNoTarget { get; }
    public abstract string LogSplitPreparing(object arg0);
    public abstract string LogSplitting { get; }
    public abstract string LogStatusCompressing { get; }
    public abstract string LogStatusCopying { get; }
    public abstract string LogStatusDecompressing { get; }
    public abstract string LogStatusDone { get; }
    public abstract string LogStatusValidating { get; }
    public abstract string LogTitleGroupDetected(object arg0);
    public abstract string LogValidating { get; }
    public abstract string LogValidationComplete { get; }
    public abstract string LogValidationStart { get; }
    public abstract string LogWriteNspFailed(object arg0);
    public abstract string MainCompressTitle { get; }
    public abstract string MainErrNoBase { get; }
    public abstract string MainErrNoFiles { get; }
    public abstract string MainErrNoKeys { get; }
    public abstract string MainErrNoKeysAndroid { get; }
    public abstract string MainErrNoOutput { get; }
    public abstract string MainLogAllComplete(object arg0);
    public abstract string MainLogAllSplitDone(object arg0);
    public abstract string MainLogElapsed(object arg0);
    public abstract string MainLogRecalculating { get; }
    public abstract string MainLogSplitStart(object arg0);
    public abstract string MainMsgDone { get; }
    public abstract string MainMsgSplitDone { get; }
    public abstract string MainSelectFolder { get; }
    public abstract string MainTitle { get; }
    public abstract string MenuOpenFolder { get; }
    public abstract string MsgTitleError { get; }
    public abstract string MsgTitleInfo { get; }
    public abstract string MsgTitleQuestion { get; }
    public abstract string MsgTitleWarning { get; }
    public abstract string SettingsCompressMode { get; }
    public abstract string SettingsForceKeyGen0 { get; }
    public abstract string SettingsTabGeneral { get; }
    public abstract string StatusAnalyzing { get; }
    public abstract string StatusNoKey { get; }
    public abstract string StatusProcessing { get; }
    public abstract string ToolTipAddFile { get; }
    public abstract string ToolTipBlockMode { get; }
    public abstract string ToolTipBlocklessMode { get; }
    public abstract string ToolTipBrowseOutput { get; }
    public abstract string ToolTipCompress { get; }
    public abstract string ToolTipCompressLevel { get; }
    public abstract string ToolTipForceKeyGen0 { get; }
    public abstract string ToolTipOpenWorkSpace { get; }
    public abstract string ToolTipRemoveAll { get; }
    public abstract string ToolTipRemoveSelected { get; }
    public abstract string ToolTipValidateCompress { get; }
}