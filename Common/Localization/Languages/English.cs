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
    public override string HeaderCompressionRatio => "Compression Ratio";
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
    public override string ToolTipClear => "Clear";
    public override string ToolTipRemoveSelected => "Delete";
    public override string ToolTipValidateCompress => "Runs an additional validation after compression";
    public override string ButtonAddFile => "Add File";
    public override string ButtonAddFolder => "Add Folder";
    public override string ButtonRemoveSelected => "Delete";
    public override string ButtonClear => "Clear";
    public override string MenuRemoveSelected => "Delete";
    public override string MenuClear => "Clear";
    public override string TabPatch => "Patch";
    public override string LabelPatch => "Patch";
    public override string TabSettings => "Settings";
    public override string LabelSettings => "Settings";
    public override string LabelVersion => "Version";
    public override string HintDropCueChd => "Drag and drop a CUE/CHD file";
    public override string HintDropSourceNsp => "Drag and drop the source NSP/NSZ file";
    public override string HintDragOrSelectWorkFolder => "Drag or select the work folder";
    public override string HintDragOrSelectOutputFolder => "Drag or select the output folder";
    public override string HintDropImageFile => "Drag and drop an image file";
    public override string HintDropFileOrFolder => "Drag and drop files or folders here";
    public override string HintDropGameFileOrFolder => "Drag and drop game files/folders here";
    public override string HintRepackReplaceRule => "Base game and update are replaced automatically; DLC is added";
    public override string HintSplitExpandRule => "Expand a combined ROM with the + button to choose the base game, update, or DLC to split";
    public override string LabelGameName => "Game name";
    public override string LabelPublisher => "Publisher";
    public override string LabelGameId => "Game ID";
    public override string LabelNspTitleId => "NSP Title ID";
    public override string LabelWideScreen => "Widescreen";
    public override string LabelSoundVolume => "Sound volume:";
    public override string LabelLz4Compression => "Lz4 compression";
    public override string LabelSupportedLanguages => "Supported languages";
    public override string LabelMultiRomIndex => "Multi-ROM index";
    public override string LabelMergeMode => "Merge mode:";
    public override string LabelEstimated => "Estimated";
    public override string LabelActual => "Actual";
    public override string LabelFileCount(object arg0) => $"{arg0} files";
    public override string HeaderFileNameCompare => "File name comparison (current/new)";
    public override string DescConvertFormats => "Converts between the file formats below";
    public override string DescRenameByFormat => "Renames files in bulk using the selected format";
    public override string ButtonForceLanguage => "Force language";
    public override string ButtonCancelForceLanguage => "Cancel forced language";
    public override string ButtonUnpack => "Unpack";
    public override string ButtonRepackFolderBased => "Repack (folder-based)";
    public override string ButtonUnpackAndRepackStreaming => "Unpack + Repack (streaming-based)";
    public override string TabRepack => "Repack";
    public override string TabMerge => "Merge";
    public override string TabSplit => "Split";
    public override string TabLz4Compression => "Lz4 compression";
    public override string TabFormatConvert => "Format conversion";
    public override string TabSaturnNspConvert => "Saturn NSP conversion";
    public override string TabIgnoreVersion => "Ignore version";
    public override string TabRename => "Rename files";
    public override string RadioMergeGroupByTitleId => "Group by Title ID (legacy)";
    public override string RadioMergeAll => "All";
    public override string MenuSelectFolder => "Select folder...";
    public override string MenuSelectArchive => "Select archive (zip/7z)...";
    public override string MenuDeselect => "Deselect";
    public override string MenuFindInFolder => "Find in folder...";
    public override string MenuFindInArchive => "Find in archive (zip/7z)...";
    public override string ToolTipTitleIdInput => "16-digit hexadecimal (e.g. 0100000000010000). Leave empty to keep the original Title ID.";
    public override string ToolTipForceLanguage => "During repack, only the selected languages are flagged on and all others are turned off.";
    public override string ToolTipMergeGroupByTitleId => "Groups items by Title ID and merges each group into a separate file.";
    public override string ToolTipMergeAll => "Merges all items into a single file.";
    public override string ToolTipRomFsLz4 => "RomFS Lz4 compression (applied on repack). The base game and update are combined into one, so checking either one enables compression.";
    public override string ToolTipBatchKoreanPatch => "Assign Korean patches in bulk (root folder or archive)";
    public override string ToolTipSelectAllToggle => "Select/deselect all";
    public override string ToolTipExpandCollapse => "Expand/Collapse";
    public override string HeaderSize => "Size";
    public override string ToolTipBrowseOutput => "Select output folder";
}