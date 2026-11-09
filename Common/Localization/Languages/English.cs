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
    public override string HintSelectOutput => "Select the output folder";

    // Header
    public override string HeaderFileName => "File Name";
    public override string HeaderFilePath => "Path";
    public override string HeaderTitle => "Title";
    public override string HeaderPatch => "Patch";
    public override string HeaderVersion => "Version";
    public override string HeaderInput => "Input";
    public override string HeaderOutput => "Output";

    // Label
    public override string LabelFile => "File";
    public override string LabelCompression => "Compression";
    public override string LabelCompressionRatio => "Compression Ratio";
    public override string LabelOutputFormat => "Output Format:";

    // Log
    public override string LogAnalyzeMetadata => "━━ Analyzing metadata... ━━";
    public override string LogCompressAndMerge => "Compressing & Merging...";
    public override string LogCompressAndSplit => "Compressing & Splitting...";
    public override string LogCompressionRatio => "Compression ratio";
    public override string LogDecompressAndMerge => "Decompressing and merging";
    public override string LogDeleteIncompleteFile => "Incomplete file was deleted.";
    public override string LogError(object arg0) => $"Error: {arg0}";
    public override string LogFinalId(object arg0, object arg1) => $"Final ID: {arg0} / Version: {arg1}";
    public override string LogMergeComplete(object arg0) => $"Merge completed: {arg0}";
    public override string LogMergeFailed(object? arg0, object arg1) => $"[{arg0}] Merge failed: {arg1}";
    public override string LogMerging => "Merging...";
    public override string LogModeCompress => "Compress";
    public override string LogModeDecompress => "Decompress";
    public override string LogProcessError => "Error processing";
    public override string LogProcessStart => "Start";
    public override string LogSplitComplete(object arg0) => $"Split complete: {arg0}";
    public override string LogSplitFailed(object arg0, object arg1) => $"Split failed (ID: {arg0}): {arg1}";
    public override string LogSplitPreparing(object arg0) => $"{arg0} Starting split";
    public override string LogSplitting => "Splitting...";
    public override string LogStatusCompressing => "Compressing...";
    public override string LogStatusCopying => "Copying...";
    public override string LogStatusDecompressing => "Decompressing...";
    public override string LogStatusDone => "Done";
    public override string LogStatusValidating => "Validating...";
    public override string LogTitleGroupDetected(object arg0) => $"{arg0} title groups detected";
    public override string LogValidationComplete => "Validation complete";
    public override string LogValidationStart => "Validation starting";

    // Main
    public override string MainErrNoFiles => "Please add files and try again.";
    public override string MainErrNoKeys => "prod.keys file not found. Please place the key file in the application folder.";
    public override string MainErrNoOutput => "Please set the output folder.";
    public override string MainLogAllComplete(object arg0) => $"✓ All tasks completed. Total time: {arg0}";
    public override string MainLogRecalculating => "Reanalyzing file types...";
    public override string MainMsgDone => "Done!";
    public override string MainMsgSplitDone => "Splitting is complete.";

    // Menu
    public override string MenuOpenFolder => "Open folder";

    // Msg
    public override string MsgTitleError => "Error";
    public override string MsgTitleInfo => "Information";
    public override string MsgTitleQuestion => "Confirmation";
    public override string MsgTitleWarning => "Warning";

    // Status
    public override string StatusAnalyzing => "Analyzing...";
    public override string StatusNoKey => "No Key";

    // ToolTip
    public override string ToolTipAddFile => "Add File";
    public override string ToolTipAddFolder => "Add Folder";
    public override string ToolTipBlocklessMode => "Sequential Access (Space Priority)";
    public override string ToolTipBlockMode => "Random Access (Speed Priority)";
    public override string ToolTipOpenWorkSpace => "Open Output Folder";
    public override string ToolTipRemoveAll => "Clear";
    public override string ToolTipRemoveSelected => "Delete";
    public override string ToolTipValidateCompress => "Runs an additional validation after compression";
}