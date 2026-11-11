namespace Common.Localization.Languages;

public sealed class ChineseTraditional : LocalizedStrings
{
    // Language
    public override string Code => "zh-TW";
    public override string DisplayName => "繁體中文";
    public override string EnglishName => "Chinese";

    // Button
    public override string ButtonAddFile => "新增檔案";
    public override string ButtonAddFolder => "新增資料夾";
    public override string ButtonAnalyze => "分析";
    public override string ButtonCalculateHash => "計算雜湊";
    public override string ButtonCancel => "取消";
    public override string ButtonCancelForceLanguage => "取消強制語言";
    public override string ButtonChange => "變更";
    public override string ButtonClear => "清空";
    public override string ButtonDownload => "下載";
    public override string ButtonFilterAll => "全部";
    public override string ButtonFilterGame => "本篇";
    public override string ButtonFilterUpdate => "更新";
    public override string ButtonForceLanguage => "強制設定語言";
    public override string ButtonInstall => "安裝";
    public override string ButtonLater => "稍後";
    public override string ButtonLoadGameList => "載入遊戲清單";
    public override string ButtonOk => "確定";
    public override string ButtonRecoverTitleDb => "還原 title.db";
    public override string ButtonRemoveSelected => "刪除選取";
    public override string ButtonRepackFolderBased => "重新打包 (以資料夾為基礎)";
    public override string ButtonReset => "重設";
    public override string ButtonSelectFile => "選擇檔案";
    public override string ButtonSettings => "設定";
    public override string ButtonStart => "開始";
    public override string ButtonUnpack => "解包";
    public override string ButtonUnpackAndRepackFull => "解包 + 重新打包 (Full)";
    public override string ButtonUnpackAndRepackStreaming => "解包 + 重新打包 (以串流為基礎)";

    // Check
    public override string CheckExtraOption => "額外選項 (cmd 0x0B)";
    public override string CheckVideoFreezeFix => "影片當機修正 (cmd 0x00)";

    // Description
    public override string DescArchivePasswordProtected => "此壓縮檔受密碼保護。";
    public override string DescAutoCompressByRule => "將依下列規則自動選擇格式並進行壓縮或解壓縮";
    public override string DescAutoMatchByExtension => "將依副檔名自動配對";
    public override string DescAutoVersionDateInName => "從修補檔名稱讀取版本與日期並顯示於結果檔名";
    public override string DescCalculateFileHash => "計算檔案雜湊";
    public override string DescCertsBinUsedForCia => "certs.bin 檔案於產生 CIA 檔案時使用";
    public override string DescClickOrDropToChange => "點擊或拖放檔案即可變更。";
    public override string DescConvertByRule => "依下列規則進行壓縮、解壓縮與格式轉換";
    public override string DescConvertByRuleMutual => "依下列規則相互轉換";
    public override string DescConvertFormats => "在下列檔案格式之間相互轉換";
    public override string DescCreateSingleTrackCue => "產生單軌 CUE 檔案 (不支援多軌)";
    public override string DescDecryptFiles => "解密檔案";
    public override string DescExtractCertsBin => "在執行檔所在位置擷取 certs.bin";
    public override string DescInstallToConsole => "將遊戲安裝到 3DS 實機";
    public override string DescMp3WavAutoConvertAt3 => "新增 MP3、WAV 檔案時會自動轉換為 AT3 格式。";
    public override string DescOutputFolderDefault => "關閉時儲存至原始 ROM 資料夾內的 output 資料夾";
    public override string DescPackMax5 => "最多可打包 5 個檔案";
    public override string DescPc98Hdi => "依邏輯路徑尋找並取代 HDI (FAT12/16) 內部檔案。掛載路徑會自動偵測。";
    public override string DescRenameByFormat => "依所選格式批次重新命名檔案";
    public override string DescRestorePbpToBinCue => "將 PBP 還原為原始 BIN+CUE";
    public override string DescSelectSdLoadList => "請選擇 SD 卡並載入安裝清單";
    public override string DescTistoryEnterUrl => "請輸入 Tistory 網址並按下分析按鈕";
    public override string DescTistoryFindAttachments => "尋找附件連結並加入清單";
    public override string DescVitaAutoClassify => "App/Patch/AddCont 會自動分類";
    public override string DescVitaGroupToNoNpDrm => "相同 Title ID 的 App/Patch/AddCont 會合併並轉換為 NoNpDrm 格式";
    public override string DescVitaKeyRequiredShared => "需要金鑰 (work.bin, *.rif/zRIF 字串) (Patch 可共用 App 的金鑰)";
    public override string DescVitaPatchMerge => "模擬器: 解密後覆寫 app/addcont / 實機: 新增 rePatch/reAddcont";
    public override string DescVitaPkgKeyRequired => "pkg 需要金鑰 (work.bin, *.rif/zRIF 字串)";
    public override string DescWiiUAutoClassify => "本篇/更新/DLC 會依 Title ID 自動分類";

    // Dialog
    public override string DialogSelectGameFile => "選擇遊戲檔案";

    // Error
    public override string ErrorNoMetadata => "沒有可解析的中繼資料。";

    // Filter
    public override string FilterAllFiles => "所有檔案";
    public override string FilterSwitchFiles => "Switch 檔案";

    // Header
    public override string HeaderCompressionRatio => "壓縮率";
    public override string HeaderConvert => "轉換";
    public override string HeaderFileName => "檔案名稱";
    public override string HeaderFileNameCompare => "檔名比較 (目前/變更後)";
    public override string HeaderFilePath => "路徑";
    public override string HeaderForcedLanguage => "固定語言";
    public override string HeaderFormat => "格式";
    public override string HeaderInput => "輸入";
    public override string HeaderKey => "金鑰";
    public override string HeaderLocalTitle => "本機標題";
    public override string HeaderOutput => "輸出";
    public override string HeaderPatch => "修補檔";
    public override string HeaderProductCode => "產品代碼";
    public override string HeaderResult => "結果";
    public override string HeaderSize => "大小";
    public override string HeaderTitle => "標題";
    public override string HeaderVersion => "版本";

    // Hint
    public override string HintDragOrClickPatch3ds => "請拖入或點擊選擇修補檔資料夾或 zip/7z 檔案";
    public override string HintDragOrClickRiivolutionPatch => "請拖入或點擊選擇 Riivolution 修補檔資料夾或 zip/7z/xml";
    public override string HintDragOrClickRom3ds => "請拖入或點擊選擇 ROM 檔案 (.3ds, .cci, .zcci, .cia)";
    public override string HintDragOrClickWiiDisc => "請拖入或點擊選擇 Wii 光碟 (.iso, .wbfs, .wia, .rvz)";
    public override string HintDragOrSelectOutputFolder => "請拖入或選擇輸出資料夾";
    public override string HintDragOrSelectWorkFolder => "請拖入或選擇工作資料夾";
    public override string HintDropArcadeFiles => "請將原始 ZIP 與修補檔 (IPS, 資料夾, ZIP) 拖放到上方";
    public override string HintDropCueChd => "請拖放 CUE/CHD 檔案";
    public override string HintDropFile => "請將檔案拖放到此處";
    public override string HintDropFileOrFolder => "請將檔案或資料夾拖放到此處";
    public override string HintDropGameFileOrFolder => "請將遊戲檔案/資料夾拖放到此處";
    public override string HintDropImageFile => "請拖放圖片檔案";
    public override string HintDropOrClickAudio => "請拖放或點擊選擇音訊檔案";
    public override string HintDropPatchFile => "請拖放修補檔";
    public override string HintDropSourceNsp => "請拖放原始 NSP/NSZ 檔案";
    public override string HintDropVitaFiles => "請將 PS Vita 檔案 (pkg, zip)/資料夾拖放到此處";
    public override string HintDropVitaPkg => "請將 PS Vita PKG 檔案/資料夾拖放到此處";
    public override string HintDropWiiUFiles => "請將 WUP/Loadiine 資料夾、wud/wux/wua 檔案拖放到此處";
    public override string HintRepackReplaceRule => "本篇/更新會自動取代，DLC 會持續新增";
    public override string HintSearchGame => "搜尋遊戲...";
    public override string HintSelectOutput => "請選擇輸出資料夾";
    public override string HintSoundCancel => "取消 (請拖放 .mp3, .wav 檔案)";
    public override string HintSoundComplete => "完成 (請拖放 .mp3, .wav 檔案)";
    public override string HintSoundError => "錯誤 (請拖放 .mp3, .wav 檔案)";
    public override string HintSplitExpandRule => "整合 ROM 可透過 + 按鈕展開，選擇本篇/更新/DLC 進行拆分";

    // Label
    public override string LabelActual => "實際";
    public override string LabelAlgorithm => "演算法:";
    public override string LabelAllFormats => "所有格式";
    public override string LabelBackgroundMusic => "背景音樂";
    public override string LabelBootLogo => "開機標誌";
    public override string LabelCompression => "壓縮";
    public override string LabelCompressionAlgorithmIso => "壓縮演算法 (ISO: PSP, PS2)";
    public override string LabelCompressionLevel => "壓縮等級";
    public override string LabelCompressionLevelNszXcz => "壓縮等級 (NSZ, XCZ)";
    public override string LabelCompressionMode => "壓縮模式";
    public override string LabelDefaultImage => "預設影像";
    public override string LabelDownloadLocation => "下載位置";
    public override string LabelEstimated => "預估";
    public override string LabelExpectedFileName => "預估檔名:";
    public override string LabelFile => "檔案";
    public override string LabelFileCount(object? arg0) => $"{arg0} 個檔案";
    public override string LabelFormat3dsCci => "3DS (與 CCI 格式相同)";
    public override string LabelFormatBinCueSingleTrack => "BIN+CUE (單軌)";
    public override string LabelFormatChdIsoBased => "CHD (以 ISO 為基礎)";
    public override string LabelFormatList3dsCciCia => "3DS (與 CCI 格式相同), CCI, CIA";
    public override string LabelFormatListIsoStandard => "ISO (標準), CUE+BIN, GDI+BIN+RAW, CCD+IMG+SUB";
    public override string LabelFormatListLoadiineFolderWua => "Loadiine 資料夾, WUA";
    public override string LabelFormatListWuaWupLoadiine => "WUA, WUP 資料夾, Loadiine 資料夾";
    public override string LabelFormatListWupFolderWua => "WUP 資料夾, WUA";
    public override string LabelFormatListWupLoadiine => "WUP 資料夾, Loadiine 資料夾";
    public override string LabelGameId => "遊戲 ID";
    public override string LabelGameName => "遊戲名稱";
    public override string LabelGeneral => "一般";
    public override string LabelLanguage => "語言";
    public override string LabelLz4Compression => "Lz4 壓縮";
    public override string LabelMergeMode => "合併方式:";
    public override string LabelMultiRomIndex => "多 ROM 索引";
    public override string LabelNoteCemuArchive => "(Cemu 封存檔)";
    public override string LabelNoteDiscImage => "(光碟映像檔)";
    public override string LabelNoteFolder => "(資料夾)";
    public override string LabelNspTitleId => "NSP Title ID";
    public override string LabelOptions => "選項:";
    public override string LabelOthers => "其他";
    public override string LabelOutputFormat => "輸出格式:";
    public override string LabelOutputOptions => "輸出選項";
    public override string LabelPatch => "修補檔";
    public override string LabelPatchInfo => "修補檔資訊";
    public override string LabelPbpPacking => "PBP 打包";
    public override string LabelPkgCount(object? arg0) => $"{arg0} 個 PKG";
    public override string LabelPopsEmulatorSettings => "POPS 模擬器設定";
    public override string LabelPublisher => "發行商";
    public override string LabelQuality => "品質";
    public override string LabelSelectPatch => "選擇修補檔:";
    public override string LabelSettings => "設定";
    public override string LabelSoundVolume => "音量:";
    public override string LabelSource => "原始檔案";
    public override string LabelSourceAutoExtractFormats => "支援自動解壓縮原始檔案的格式:";
    public override string LabelSupportedLanguages => "支援的語言";
    public override string LabelSupportedPatchFormats => "支援的修補檔格式:";
    public override string LabelTistoryUrl => "Tistory 網址";
    public override string LabelVersion => "版本";
    public override string LabelWideScreen => "寬螢幕";

    // Log
    public override string LogAnalyzeMetadata => "━━ 正在解析中繼資料... ━━";
    public override string LogCompressAndMerge => "正在壓縮並合併...";
    public override string LogCompressAndSplit => "正在壓縮並拆分...";
    public override string LogCompressionRatio => "壓縮率";
    public override string LogDecompressAndMerge => "正在解壓並合併";
    public override string LogDeleteIncompleteFile => "已刪除未完整產生的檔案。";
    public override string LogError(object? arg0) => $"發生錯誤: {arg0}";
    public override string LogFinalId(object? arg0, object? arg1) => $"最終 ID: {arg0} / 版本: {arg1}";
    public override string LogMergeComplete(object? arg0) => $"合併完成: {arg0}";
    public override string LogMergeFailed(object? arg0, object? arg1) => $"[{arg0}] 合併失敗: {arg1}";
    public override string LogMerging => "正在合併...";
    public override string LogModeCompress => "壓縮";
    public override string LogModeDecompress => "解壓";
    public override string LogProcessError => "處理錯誤";
    public override string LogProcessStart => "開始";
    public override string LogSplitComplete(object? arg0) => $"拆分完成: {arg0}";
    public override string LogSplitFailed(object? arg0, object? arg1) => $"拆分失敗 (ID: {arg0}): {arg1}";
    public override string LogSplitPreparing(object? arg0) => $"{arg0} 開始拆分";
    public override string LogSplitting => "正在拆分...";
    public override string LogStatusCompressing => "壓縮中...";
    public override string LogStatusCopying => "複製中...";
    public override string LogStatusDecompressing => "解壓中...";
    public override string LogStatusDone => "完成";
    public override string LogStatusValidating => "驗證中...";
    public override string LogTitleGroupDetected(object? arg0) => $"偵測到 {arg0} 個標題群組";
    public override string LogValidationComplete => "驗證完成";
    public override string LogValidationStart => "開始驗證";

    // Main
    public override string MainErrNoFiles => "請新增檔案後再試一次。";
    public override string MainErrNoKeys => "找不到 prod.keys 檔案。請將金鑰檔案放入應用程式資料夾中。";
    public override string MainErrNoOutput => "請設定輸出資料夾。";
    public override string MainLogAllComplete(object? arg0) => $"✓ 所有任務已完成。總耗時: {arg0}";
    public override string MainLogRecalculating => "正在重新分析檔案類型...";
    public override string MainMsgDone => "完成！";
    public override string MainMsgSplitDone => "拆分已完成。";

    // Menu
    public override string MenuClear => "清空";
    public override string MenuClearLog => "清除記錄";
    public override string MenuCopyLog => "複製記錄";
    public override string MenuDeselect => "取消選取";
    public override string MenuExtractCci => "擷取為 CCI";
    public override string MenuExtractCia => "擷取為 CIA";
    public override string MenuFindInArchive => "在壓縮檔中尋找 (zip/7z)...";
    public override string MenuFindInFolder => "在資料夾中尋找...";
    public override string MenuOpenFolder => "開啟資料夾";
    public override string MenuRemoveSelected => "刪除選取";
    public override string MenuSelectArchive => "選擇壓縮檔 (zip/7z)...";
    public override string MenuSelectFileIpsZip => "選擇檔案 (*.ips, *.zip)...";
    public override string MenuSelectFileZip7z => "選擇檔案 (*.zip, *.7z)...";
    public override string MenuSelectFileZip7zXml => "選擇檔案 (*.zip, *.7z, *.xml)...";
    public override string MenuSelectFolder => "選擇資料夾...";

    // Message
    public override string MsgTitleError => "錯誤";
    public override string MsgTitleInfo => "資訊";
    public override string MsgTitleQuestion => "確認";
    public override string MsgTitleWarning => "警告";

    // Option
    public override string OptionLevel0None => "0: 不壓縮";
    public override string OptionLevel18Recommended => "18: 建議";
    public override string OptionLevel3Fastest => "3: 最快速度";
    public override string OptionLevel5Balanced => "5: 均衡";
    public override string OptionLevel9Balanced => "9: 均衡";
    public override string OptionLevel9Max => "9: 最大";

    // Radio
    public override string RadioBlock => "區塊";
    public override string RadioBlockless => "無區塊";
    public override string RadioCciEmulator => "CCI (模擬器)";
    public override string RadioCiaConsole => "CIA (實機)";
    public override string RadioConsoleRepatch => "實機 (rePatch, reAddcont)";
    public override string RadioEmulatorAppAddcont => "模擬器 (app, addcont)";
    public override string RadioFolderPerTitleId => "資料夾 (依 Title ID)";
    public override string RadioLowercase => "小寫";
    public override string RadioMergeAll => "全部";
    public override string RadioMergeGroupByTitleId => "依 Title ID 分組 (原有方式)";
    public override string RadioOutputGameId => "實機 (Title ID/EBOOT.PBP)";
    public override string RadioOutputNormal(object? arg0) => $"一般 (標題名稱.{arg0})";
    public override string RadioQuality30s => "30 秒 (132k)";
    public override string RadioQuality60s => "60 秒 (66k)";
    public override string RadioRvzDolphin => "RVZ (Dolphin 專用壓縮)";
    public override string RadioUppercase => "大寫";
    public override string RadioWbfsUsbLoader => "WBFS (USB 載入器)";
    public override string RadioWuaEmulator => "WUA (模擬器)";
    public override string RadioWupConsole => "WUP (實機)";
    public override string RadioZcciAzahar => "ZCCI (Azahar 專用壓縮)";
    public override string RadioZipPerTitleId => "ZIP (依 Title ID)";

    // Status
    public override string StatusAnalyzing => "正在解析...";
    public override string StatusConvertingToAt3 => "正在轉換為 AT3...";
    public override string StatusDownloadingImage => "正在下載圖片...";
    public override string StatusHashCancelledByPatch => "因開始修補，雜湊計算已取消";
    public override string StatusHashFailed => "雜湊計算失敗";
    public override string StatusLoading => "載入中...";
    public override string StatusMatched => "已配對";
    public override string StatusNoKey => "無金鑰";
    public override string StatusPresetLoaded => "已載入預設 (手動設定已停用)";
    public override string StatusUnmatched => "未配對";
    public override string StatusWrongPassword => "密碼不正確。";

    // Tab
    public override string TabArcade => "街機";
    public override string TabCalculateHash => "雜湊計算";
    public override string TabConvert => "轉換";
    public override string TabCreateCue => "產生 Cue";
    public override string TabDecrypt => "解密";
    public override string TabExtractCertsBin => "擷取 certs.bin";
    public override string TabFormatConvert => "格式轉換";
    public override string TabGeneral => "一般";
    public override string TabIgnoreVersion => "忽略版本";
    public override string TabInstall => "安裝";
    public override string TabInstalledGames => "已安裝的遊戲清單";
    public override string TabInstallNewGame => "安裝新遊戲";
    public override string TabKeyFile => "金鑰檔案";
    public override string TabLz4Compression => "Lz4 壓縮";
    public override string TabMerge => "合併";
    public override string TabPatch => "修補檔";
    public override string TabPbpPacking => "PBP 打包";
    public override string TabPbpUnpack => "PBP 解包";
    public override string TabRename => "變更檔名";
    public override string TabRepack => "重新打包";
    public override string TabSaturnNspConvert => "Saturn NSP 轉換";
    public override string TabSettings => "設定";
    public override string TabSplit => "拆分";
    public override string TabTistoryDownloader => "Tistory 下載器";
    public override string TabUtil => "工具";

    // Title
    public override string TitleArchivePassword => "壓縮檔密碼";

    // Toggle
    public override string ToggleAlwaysOnTop => "永遠顯示在其他視窗上方";
    public override string ToggleAutoCompressAfterPatch => "修補後自動壓縮";
    public override string ToggleAutoExtractDelete => "自動解壓縮 (完成後刪除原始檔案)";
    public override string ToggleAutoVersionDateInName => "在結果檔名中自動標示版本/日期";
    public override string ToggleChangeOutputFolder => "變更輸出資料夾";
    public override string ToggleCheckUpdateOnStart => "啟動時檢查更新";
    public override string ToggleMergePatchToGame => "將修補檔合併到遊戲";
    public override string ToggleOtherFormats => "其他";
    public override string ToggleSoundEffects => "音效";

    // ToolTip
    public override string ToolTipAddFile => "新增檔案";
    public override string ToolTipAddFolder => "新增資料夾";
    public override string ToolTipAddSequentialPatch => "新增要連續套用的修補檔";
    public override string ToolTipAutoExtractDelete => "如果下載的檔案是壓縮檔 (zip/7z/rar)，將自動解壓縮並刪除原始壓縮檔";
    public override string ToolTipBatchKoreanPatch => "批次指定韓文修補檔 (根資料夾或壓縮檔)";
    public override string ToolTipBlocklessMode => "循序存取（容量優先）";
    public override string ToolTipBlockMode => "隨機存取（速度優先）";
    public override string ToolTipBrowseOutput => "選擇輸出資料夾";
    public override string ToolTipChangePatchFile => "變更為其他修補檔";
    public override string ToolTipChangePatchPackage => "變更為其他修補檔套件";
    public override string ToolTipClear => "清空";
    public override string ToolTipCopyHash => "複製雜湊值";
    public override string ToolTipDragFolderToChangeDownload => "將資料夾拖曳至此即可變更下載位置。";
    public override string ToolTipExpandCollapse => "展開/摺疊";
    public override string ToolTipForcedLanguage => "安裝時將地區設定固定為此語言 (自動產生 locale.txt)。僅顯示 SMDH 中實際存在的語言。";
    public override string ToolTipForceLanguage => "重新打包時，僅啟用所選語言的旗標，其餘全部關閉。";
    public override string ToolTipGameNameMax63 => "遊戲名稱 (最多 63 個字元)";
    public override string ToolTipLicenseKey => "授權檔案路徑 (work.bin, *.rif)/zRIF 字串";
    public override string ToolTipLicenseKeyPatchFallback => "授權檔案路徑 (work.bin, *.rif)/zRIF 字串 (Patch 留空時使用 App 的金鑰)";
    public override string ToolTipMergeAll => "將所有項目合併為單一檔案。";
    public override string ToolTipMergeGroupByTitleId => "依 Title ID 將項目分組，並分別合併為獨立檔案。";
    public override string ToolTipMovableSedGuide => "1. 執行 GodMode9 (START + POWER)\n2. 選擇 SYSNAND CTRNAND → private → movable.sed\n3. 執行最下方的 Copy to 0:/gm9/out";
    public override string ToolTipOpenGithubReleases => "開啟 GitHub 發行頁面";
    public override string ToolTipOpenHelp => "開啟說明頁面";
    public override string ToolTipOpenWorkSpace => "開啟輸出資料夾";
    public override string ToolTipPlayPause => "播放/暫停";
    public override string ToolTipPublisherMax63 => "發行商 (最多 63 個字元)";
    public override string ToolTipRecoverTitleDb => "當 3DS 實機因 title.db 錯誤導致已安裝的遊戲未顯示於 HOME 選單或無法辨識時使用。\n\n以 SD 卡中已安裝的遊戲為準，重新建立 title.db 與 import.db。\n原有的 title.db/import.db 將被刪除，遊戲檔案不會被修改。\n若遊戲已被正常辨識，則無需按下。";
    public override string ToolTipRemoveSelected => "刪除選取";
    public override string ToolTipRestoreDefaultPath => "還原預設位置";
    public override string ToolTipRomFsLz4 => "RomFS Lz4 壓縮 (重新打包時套用)。本篇與更新會合併為一個，因此勾選其中任一個即會壓縮。";
    public override string ToolTipSelectAllToggle => "全選/取消全選";
    public override string ToolTipSelectFile => "選擇檔案";
    public override string ToolTipTistoryUrlInput => "每行輸入一個 Tistory 文章網址 (可輸入多個，按 Ctrl+Enter 開始分析)";
    public override string ToolTipTitleIdInput => "16 位十六進位數字 (例如: 0100000000010000)。留空則保留原始 Title ID。";
    public override string ToolTipValidateCompress => "壓縮後執行額外驗證";

    // Update
    public override string LatestVersion => "最新版本";
    public override string NoUpdateNotes => "沒有更新內容。";
    public override string UpdateAvailable => "有可用的更新";
    public override string UpdateDescription => "RomForge 已發布新版本。";
    public override string UpdateTitle => "RomForge 更新";

    public override string StatusWaiting => "等待中";
    public override string StatusConverting => "轉換中";
    public override string StatusDecrypting => "解密中";
    public override string StatusExtracting => "提取中";
    public override string StatusDownloading => "下載中";
    public override string StatusUnzipping => "解壓縮中";
    public override string StatusUnzipDone => "解壓縮完成";
    public override string StatusUnzipFailed => "解壓縮失敗";
    public override string StatusDone => "完成";
    public override string StatusFailed => "失敗";
    public override string StatusCancelled => "已取消";
    public override string StatusUnsupported => "不支援";
    public override string StatusSkipped => "已略過";
    public override string RenameStatusReady => "將重新命名";
    public override string RenameStatusUnchanged => "無變更";
    public override string RenameStatusMissingInfo => "無資訊";
    public override string RenameStatusEmptyName => "無名稱";
    public override string RenameStatusNameTooLong => "名稱過長";
    public override string RenameStatusDuplicate => "名稱重複";
    public override string RenameStatusTargetExists => "檔案已存在";

    public override string ProgressElapsed(object? arg0) => $"已耗時 {arg0}";
    public override string ProgressCompletedCount(object? arg0, object? arg1) => $"{arg0} / {arg1} 完成";
    public override string ProgressHashingPercent(object? arg0) => $"正在計算雜湊... {arg0}%";
    public override string ProgressAnalyzingPatchInfo => "正在分析修補檔資訊...";
    public override string ProgressExtractingPatch => "正在解壓縮修補檔...";
    public override string ProgressUnpackingFile(object? arg0) => $"正在解包: {arg0}";
    public override string ProgressRepackingFile(object? arg0) => $"正在重新打包: {arg0}";
    public override string ProgressApplyingPatchTo(object? arg0) => $"正在套用修補檔: {arg0}";
    public override string ProgressHashingPartitions(object? arg0) => $"正在計算分割區雜湊: {arg0}";
    public override string ProgressCompressingWbfs(object? arg0) => $"正在壓縮 WBFS: {arg0}";
    public override string ProgressCompressingRvz(object? arg0) => $"正在壓縮 RVZ: {arg0}";
    public override string ProgressCreatingIso(object? arg0) => $"正在建立 ISO: {arg0}";
    public override string ProgressWupCreated(object? arg0, object? arg1) => $"WUP 建立完成: {arg0}_v{arg1}";
    public override string MsgCancelled => "已取消。";
    public override string StatusInitializing => "正在初始化...";
    public override string StatusSelectSdAndMovable => "請選擇 SD 卡和 movable.sed";
    public override string ProgressLoadingCount(object? arg0, object? arg1) => $"正在載入... {arg0} / {arg1}";
    public override string ProgressScanningCount(object? arg0, object? arg1) => $"正在掃描... {arg0} / {arg1}";
    public override string StatusRecoveringTitleDb => "正在還原 title.db...";
    public override string StatusRecoveryDone(object? arg0) => $"還原完成: {arg0} 個標題";
    public override string StatusExtractingTitle(object? arg0, object? arg1) => $"正在提取: {arg0} - {arg1}";
    public override string StatusInstallStarted(object? arg0) => $"開始安裝: {arg0}";
    public override string StatusInstallDone(object? arg0) => $"安裝完成: {arg0}";
    public override string StatusScanningTitles => "正在掃描標題...";
    public override string StatusLoadingMetadata(object? arg0) => $"正在載入中繼資料... (共 {arg0} 個)";
    public override string StatusTitlesLoaded(object? arg0) => $"已載入 {arg0} 個標題";
    public override string ProgressPreparing => "準備中";
    public override string ProgressHashing => "計算雜湊中";
    public override string ProgressCompressingNsz => "NSZ 壓縮中";

    public override string StatusUnpacking => "正在解包";
    public override string StatusApplyingPatch => "正在套用修補檔";
    public override string StatusApplyingPatchAndCompressing => "正在套用修補檔並壓縮";
    public override string StatusLoadingSource => "正在載入來源檔案...";
    public override string StatusInstalling => "正在安裝...";
    public override string StatusExtractingCia => "正在提取 CIA...";
    public override string ProgressHashingNca => "計算 NCA 雜湊中";
    public override string ProgressCompressingRomfs => "RomFS 壓縮中";
    public override string ProgressBuildingPfs0 => "PFS0 建置中";
    public override string ProgressBuildingRomfs => "RomFS 建置中";
    public override string ProgressWritingRomfs => "RomFS 寫入中";
    public override string ProgressCreatingIvfc(object? arg0, object? arg1) => $"正在建立 IVFC... {arg0}/{arg1}";
    public override string ProgressFinalizingNsp => "正在完成 NSP...";
    public override string ProgressBuildingProgram => "正在建置程式...";
    public override string ProgressApplyingExefsPatches(object? arg0) => $"正在套用 exefs_patches... ({arg0})";
    public override string ProgressApplyingLabel(object? arg0) => $"正在套用 {arg0}...";
    public override string LabelDlcPatch(object? arg0) => $"DLC 修補檔 ({arg0})";
    public override string LabelKoreanPatch => "韓文修補檔";
    public override string ProgressUnpackingNcaEntry(object? arg0, object? arg1) => $"正在解包 {arg0} - {arg1}";
    public override string ProgressExtractDone => "提取完成";
    public override string ProgressCompressDone => "壓縮完成";
    public override string ProgressConvertingBinCue => "BIN/CUE 轉換中";
    public override string ProgressConvertingIso => "ISO 轉換中";
    public override string ProgressPacking => "打包中";
    public override string ProgressScanningTitle(object? arg0) => $"正在掃描: {arg0}";
    public override string ProgressScanError(object? arg0, object? arg1) => $"錯誤 ({arg0}): {arg1}";
    public override string ProgressExtractingIso => "正在提取 ISO...";
    public override string ProgressCopyingTrack(object? arg0, object? arg1) => $"正在複製軌道 ({arg0}/{arg1})";
    public override string ProgressConvertingChd => "正在轉換為 CHD...";
    public override string ProgressCompressingZcci => "正在壓縮 ZCCI...";
    public override string ProgressConvertingFormat => "正在轉換格式...";
    public override string ProgressPatchDone => "修補檔完成";
    public override string ProgressExtractingSource => "正在解壓縮來源檔案...";
    public override string ProgressGdParsingMetadata => "正在解析 GDI 中繼資料與來源結構...";
    public override string ProgressGdPatchAllDone => "所有修補檔套用工作已完成！";
    public override string ProgressGdExtractingDcp(object? arg0) => $"正在解壓縮 DCP 套件: {arg0}";
    public override string ProgressGdApplyingXdelta(object? arg0) => $"正在套用 xdelta 差異修補檔: {arg0}";
    public override string ProgressGdPreparingMedia => "正在準備解析現有 GD-ROM 媒體...";
    public override string ProgressApplyingPatchCount(object? arg0, object? arg1) => $"正在套用修補檔 ({arg0}/{arg1})";
    public override string ProgressGdRelocatingIso9660(object? arg0, object? arg1) => $"正在重新排列 ISO9660 目錄表與結構 ({arg0}/{arg1})";
    public override string ProgressGdMergingSectors => "開始合併並整理重複磁區...";
    public override string ProgressGdCreatingTrack(object? arg0, object? arg1) => $"正在建立 {arg0}: {arg1}";
    public override string ProgressGdAnalyzingTracks => "正在分析其餘軌道...";
    public override string ProgressGdCopyingTrack(object? arg0, object? arg1, object? arg2) => $"正在複製資源軌道: {arg0} ({arg1}MB / {arg2}MB)";
    public override string ProgressGdUpdatingIndex => "正在最終驗證並更新 GDI 索引檔案...";
    public override string ProgressGdRebuildAllDone => "所有 GD-ROM 重建工作已完成！";
    public override string ProgressGdAligningSectors => "正在整理資料軌道磁區...";
    public override string ProgressGdWritingRawSectors => "正在產生並寫入資料軌道原始磁區...";
    public override string ProgressGdTrackWritten => "資料軌道寫入完成。";
}