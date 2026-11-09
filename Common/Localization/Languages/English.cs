namespace Common.Localization.Languages;

public sealed class English : LocalizedStrings
{
    public override string Code => "en-US";
    public override string DisplayName => "English";
    public override string EnglishName => "English";

    // Button
    public override string ButtonCancel => "Cancel";
    public override string ButtonSettings => "Settings";
    public override string ButtonStart => "Start";

    // Dialog
    public override string DialogSelectGameFile => "Select Game File";

    // Error
    public override string ErrorNoMetadata => "No analyzable metadata found.";

    // Filter
    public override string FilterAllFiles => "All Files";
    public override string FilterSwitchFiles => "Switch Files";

    // Hint
    public override string HintSelectOutput => "Select the output folder.";

    // Header
    public override string HeaderFileName => "File Name";
    public override string HeaderFilePath => "Path";

    // Label
    public override string LabelCompression => "Compression";
    public override string LabelCompressionRatio => "Compression Ratio";

    // Log
    public override string LogAnalyzeMetadata => "━━ Analyzing metadata... ━━";
    public override string LogBaseMissingSkip(object arg0) => $"[{arg0}] Base not found - skipped";
    public override string LogCompressAndMerge => "Compressing & Merging...";
    public override string LogCompressAndSplit => "Compressing & Splitting...";
    public override string LogCompressionRatio => "Compression ratio";
    public override string LogDecompressAndMerge => "Decompressing and merging";
    public override string LogDecompressAndSplit => "Decompressing and splitting";
    public override string LogDeleteIncompleteFile => "Incomplete file was deleted.";
    public override string LogError(object arg0) => $"Error: {arg0}";
    public override string LogFinalId(object arg0, object arg1) => $"Final ID: {arg0} / Version: {arg1}";
    public override string LogMergeComplete(object arg0) => $"Merge completed: {arg0}";
    public override string LogMergeFailed(object? arg0, object arg1) => $"[{arg0}] Merge failed: {arg1}";
    public override string LogMergeStart => "━━ Starting Super NSP merge ━━";
    public override string LogMerging => "Merging...";
    public override string LogModeCompress => "Compress";
    public override string LogModeDecompress => "Decompress";
    public override string LogProcessError => "Error processing";
    public override string LogProcessStart => "Start";
    public override string LogSplitComplete(object arg0) => $"split complete: {arg0}";
    public override string LogSplitDecompressing(object arg0) => $"- {arg0} Decompressing and splitting...";
    public override string LogSplitExtracting(object arg0) => $"- {arg0} Splitting...";
    public override string LogSplitFailed(object arg0, object arg1) => $"Split failed (ID: {arg0}): {arg1}";
    public override string LogSplitNoTarget => "Already a single content or no items to split.";
    public override string LogSplitPreparing(object arg0) => $"{arg0} Starting splitting";
    public override string LogSplitting => "Separating...";
    public override string LogStatusCompressing => "Compressing...";
    public override string LogStatusCopying => "Copying...";
    public override string LogStatusDecompressing => "Decompressing...";
    public override string LogStatusDone => "Done";
    public override string LogStatusValidating => "Validating...";
    public override string LogTitleGroupDetected(object arg0) => $"{arg0} title groups detected";
    public override string LogValidating => "Validating...";
    public override string LogValidationComplete => "Validation complete";
    public override string LogValidationStart => "Validation starting";
    public override string LogWriteNspFailed(object arg0) => $"Failed to write NSP: {arg0}";

    // Main
    public override string MainCompressTitle => "NSW Compression Tool";
    public override string MainErrNoBase => "Please add a file containing the Base game.";
    public override string MainErrNoFiles => "Please add files and try again.";
    public override string MainErrNoKeys => "prod.keys file not found. Please place the key file in the application folder.";
    public override string MainErrNoKeysAndroid => "prod.keys file not found. Please place the key file in the Download folder.";
    public override string MainErrNoOutput => "Please set the output folder.";
    public override string MainLogAllComplete(object arg0) => $"✓ All tasks completed. Total time: {arg0}";
    public override string MainLogAllSplitDone(object arg0) => $"✓ All split tasks completed! Total time: {arg0}";
    public override string MainLogElapsed(object arg0) => $"{arg0} elapsed";
    public override string MainLogRecalculating => "Reanalyzing file types...";
    public override string MainLogSplitStart(object arg0) => $"━━ Starting Split Analysis: {arg0} ━━";
    public override string MainMsgDone => "Done!";
    public override string MainMsgSplitDone => "Splitting is complete.";
    public override string MainSelectFolder => "Select Output Folder";
    public override string MainTitle => "NSW Merge Tool";

    // Menu
    public override string MenuOpenFolder => "Open folder";

    // Msg
    public override string MsgTitleError => "Error";
    public override string MsgTitleInfo => "Information";
    public override string MsgTitleQuestion => "Confirmation";
    public override string MsgTitleWarning => "Warning";

    // Settings
    public override string SettingsCompressMode => "Compression Mode";
    public override string SettingsForceKeyGen0 => "Force KeyGen 0";
    public override string SettingsTabGeneral => "General";

    // Status
    public override string StatusAnalyzing => "Analyzing...";
    public override string StatusNoKey => "No Key";
    public override string StatusProcessing => "Processing...";

    // ToolTip
    public override string ToolTipAddFile => "Add File";
    public override string ToolTipAddFolder => "Add Folder";
    public override string ToolTipBlocklessMode => "Sequential Access (Space Priority)";
    public override string ToolTipBlockMode => "Random Access (Speed Priority)";
    public override string ToolTipBrowseOutput => "Select Output Folder";
    public override string ToolTipCompress => "Compress to NSZ when checked (merge only)";
    public override string ToolTipCompressLevel => "3: Fastest | 9: Balanced | 18: Recommended";
    public override string ToolTipForceKeyGen0 => "Force Firmware KeyGen to 0";
    public override string ToolTipOpenWorkSpace => "Open Output Folder";
    public override string ToolTipRemoveAll => "Clear";
    public override string ToolTipRemoveSelected => "Delete";
    public override string ToolTipValidateCompress => "Runs an additional validation after compression";
}