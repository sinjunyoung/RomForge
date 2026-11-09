namespace Common.Localization.Languages;

public sealed class ChineseTraditional : LocalizedStrings
{
    public override string Code => "zh-TW";
    public override string DisplayName => "繁體中文";
    public override string EnglishName => "Chinese";

    // Button
    public override string ButtonCancel => "取消";
    public override string ButtonSettings => "設定";
    public override string ButtonStart => "開始";

    // Dialog
    public override string DialogSelectGameFile => "選擇遊戲檔案";

    // Error
    public override string ErrorNoMetadata => "沒有可解析的中繼資料。";

    // Filter
    public override string FilterAllFiles => "所有檔案";
    public override string FilterSwitchFiles => "Switch 檔案";

    // Hint
    public override string HintSelectOutput => "請選擇輸出資料夾";

    // Header
    public override string HeaderFileName => "檔案名稱";
    public override string HeaderFilePath => "路徑";
    public override string HeaderTitle => "標題";
    public override string HeaderPatch => "修補檔";
    public override string HeaderVersion => "版本";
    public override string HeaderInput => "輸入";
    public override string HeaderOutput => "輸出";

    // Label
    public override string LabelFile => "檔案";
    public override string LabelCompression => "壓縮";
    public override string LabelCompressionRatio => "壓縮率";
    public override string LabelOutputFormat => "輸出格式:";

    // Log
    public override string LogAnalyzeMetadata => "━━ 正在解析中繼資料... ━━";
    public override string LogCompressAndMerge => "正在壓縮並合併...";
    public override string LogCompressAndSplit => "正在壓縮並拆分...";
    public override string LogCompressionRatio => "壓縮率";
    public override string LogDecompressAndMerge => "正在解壓並合併";
    public override string LogDeleteIncompleteFile => "已刪除未完整產生的檔案。";
    public override string LogError(object arg0) => $"發生錯誤: {arg0}";
    public override string LogFinalId(object arg0, object arg1) => $"最終 ID: {arg0} / 版本: {arg1}";
    public override string LogMergeComplete(object arg0) => $"合併完成: {arg0}";
    public override string LogMergeFailed(object? arg0, object arg1) => $"[{arg0}] 合併失敗: {arg1}";
    public override string LogMerging => "正在合併...";
    public override string LogModeCompress => "壓縮";
    public override string LogModeDecompress => "解壓";
    public override string LogProcessError => "處理錯誤";
    public override string LogProcessStart => "開始";
    public override string LogSplitComplete(object arg0) => $"拆分完成: {arg0}";
    public override string LogSplitFailed(object arg0, object arg1) => $"拆分失敗 (ID: {arg0}): {arg1}";
    public override string LogSplitPreparing(object arg0) => $"{arg0} 開始拆分";
    public override string LogSplitting => "正在拆分...";
    public override string LogStatusCompressing => "壓縮中...";
    public override string LogStatusCopying => "複製中...";
    public override string LogStatusDecompressing => "解壓中...";
    public override string LogStatusDone => "完成";
    public override string LogStatusValidating => "驗證中...";
    public override string LogTitleGroupDetected(object arg0) => $"偵測到 {arg0} 個標題群組";
    public override string LogValidationComplete => "驗證完成";
    public override string LogValidationStart => "開始驗證";

    // Main
    public override string MainErrNoFiles => "請新增檔案後再試一次。";
    public override string MainErrNoKeys => "找不到 prod.keys 檔案。請將金鑰檔案放入應用程式資料夾中。";
    public override string MainErrNoOutput => "請設定輸出資料夾。";
    public override string MainLogAllComplete(object arg0) => $"✓ 所有任務已完成。總耗時: {arg0}";
    public override string MainLogRecalculating => "正在重新分析檔案類型...";
    public override string MainMsgDone => "完成！";
    public override string MainMsgSplitDone => "拆分已完成。";

    // Menu
    public override string MenuOpenFolder => "開啟資料夾";

    // Msg
    public override string MsgTitleError => "錯誤";
    public override string MsgTitleInfo => "資訊";
    public override string MsgTitleQuestion => "確認";
    public override string MsgTitleWarning => "警告";

    // Status
    public override string StatusAnalyzing => "正在解析...";
    public override string StatusNoKey => "無金鑰";

    // ToolTip
    public override string ToolTipAddFile => "新增檔案";
    public override string ToolTipAddFolder => "新增資料夾";
    public override string ToolTipBlocklessMode => "循序存取（容量優先）";
    public override string ToolTipBlockMode => "隨機存取（速度優先）";
    public override string ToolTipOpenWorkSpace => "開啟輸出資料夾";
    public override string ToolTipRemoveAll => "清空";
    public override string ToolTipRemoveSelected => "刪除選取";
    public override string ToolTipValidateCompress => "壓縮後執行額外驗證";
}