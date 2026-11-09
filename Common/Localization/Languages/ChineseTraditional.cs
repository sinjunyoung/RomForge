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
    public override string HeaderCompressionRatio => "壓縮率";
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
    public override string ToolTipClear => "清空";
    public override string ToolTipRemoveSelected => "刪除選取";
    public override string ToolTipValidateCompress => "壓縮後執行額外驗證";
    public override string ButtonAddFile => "新增檔案";
    public override string ButtonAddFolder => "新增資料夾";
    public override string ButtonRemoveSelected => "刪除選取";
    public override string ButtonClear => "清空";
    public override string MenuRemoveSelected => "刪除選取";
    public override string MenuClear => "清空";
    public override string TabPatch => "修補檔";
    public override string LabelPatch => "修補檔";
    public override string TabSettings => "設定";
    public override string LabelSettings => "設定";
    public override string LabelVersion => "版本";
    public override string HintDropCueChd => "請拖放 CUE/CHD 檔案";
    public override string HintDropSourceNsp => "請拖放原始 NSP/NSZ 檔案";
    public override string HintDragOrSelectWorkFolder => "請拖入或選擇工作資料夾";
    public override string HintDragOrSelectOutputFolder => "請拖入或選擇輸出資料夾";
    public override string HintDropImageFile => "請拖放圖片檔案";
    public override string HintDropFileOrFolder => "請將檔案或資料夾拖放到此處";
    public override string HintDropGameFileOrFolder => "請將遊戲檔案/資料夾拖放到此處";
    public override string HintRepackReplaceRule => "本篇/更新會自動取代，DLC 會持續新增";
    public override string HintSplitExpandRule => "整合 ROM 可透過 + 按鈕展開，選擇本篇/更新/DLC 進行拆分";
    public override string LabelGameName => "遊戲名稱";
    public override string LabelPublisher => "發行商";
    public override string LabelGameId => "遊戲 ID";
    public override string LabelNspTitleId => "NSP Title ID";
    public override string LabelWideScreen => "寬螢幕";
    public override string LabelSoundVolume => "音量:";
    public override string LabelLz4Compression => "Lz4 壓縮";
    public override string LabelSupportedLanguages => "支援的語言";
    public override string LabelMultiRomIndex => "多 ROM 索引";
    public override string LabelMergeMode => "合併方式:";
    public override string LabelEstimated => "預估";
    public override string LabelActual => "實際";
    public override string LabelFileCount(object arg0) => $"{arg0} 個檔案";
    public override string HeaderFileNameCompare => "檔名比較 (目前/變更後)";
    public override string DescConvertFormats => "在下列檔案格式之間相互轉換";
    public override string DescRenameByFormat => "依所選格式批次重新命名檔案";
    public override string ButtonForceLanguage => "強制設定語言";
    public override string ButtonCancelForceLanguage => "取消強制語言";
    public override string ButtonUnpack => "解包";
    public override string ButtonRepackFolderBased => "重新打包 (以資料夾為基礎)";
    public override string ButtonUnpackAndRepackStreaming => "解包 + 重新打包 (以串流為基礎)";
    public override string TabRepack => "重新打包";
    public override string TabMerge => "合併";
    public override string TabSplit => "拆分";
    public override string TabLz4Compression => "Lz4 壓縮";
    public override string TabFormatConvert => "格式轉換";
    public override string TabSaturnNspConvert => "Saturn NSP 轉換";
    public override string TabIgnoreVersion => "忽略版本";
    public override string TabRename => "變更檔名";
    public override string RadioMergeGroupByTitleId => "依 Title ID 分組 (原有方式)";
    public override string RadioMergeAll => "全部";
    public override string MenuSelectFolder => "選擇資料夾...";
    public override string MenuSelectArchive => "選擇壓縮檔 (zip/7z)...";
    public override string MenuDeselect => "取消選取";
    public override string MenuFindInFolder => "在資料夾中尋找...";
    public override string MenuFindInArchive => "在壓縮檔中尋找 (zip/7z)...";
    public override string ToolTipTitleIdInput => "16 位十六進位數字 (例如: 0100000000010000)。留空則保留原始 Title ID。";
    public override string ToolTipForceLanguage => "重新打包時，僅啟用所選語言的旗標，其餘全部關閉。";
    public override string ToolTipMergeGroupByTitleId => "依 Title ID 將項目分組，並分別合併為獨立檔案。";
    public override string ToolTipMergeAll => "將所有項目合併為單一檔案。";
    public override string ToolTipRomFsLz4 => "RomFS Lz4 壓縮 (重新打包時套用)。本篇與更新會合併為一個，因此勾選其中任一個即會壓縮。";
    public override string ToolTipBatchKoreanPatch => "批次指定韓文修補檔 (根資料夾或壓縮檔)";
    public override string ToolTipSelectAllToggle => "全選/取消全選";
    public override string ToolTipExpandCollapse => "展開/摺疊";
    public override string HeaderSize => "大小";
    public override string ToolTipBrowseOutput => "選擇輸出資料夾";
}