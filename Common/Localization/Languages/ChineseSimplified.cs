namespace Common.Localization.Languages;

public sealed class ChineseSimplified : LocalizedStrings
{
    public override string Code => "zh-CN";
    public override string DisplayName => "简体中文";
    public override string EnglishName => "Chinese";

    // Button
    public override string ButtonCancel => "取消";
    public override string ButtonSettings => "设置";
    public override string ButtonStart => "开始";

    // Dialog
    public override string DialogSelectGameFile => "选择游戏文件";

    // Error
    public override string ErrorNoMetadata => "没有可解析的元数据。";

    // Filter
    public override string FilterAllFiles => "所有文件";
    public override string FilterSwitchFiles => "Switch 文件";

    // Hint
    public override string HintSelectOutput => "请选择输出文件夹";

    // Header
    public override string HeaderFileName => "文件名";
    public override string HeaderFilePath => "路径";
    public override string HeaderTitle => "标题";
    public override string HeaderPatch => "补丁";
    public override string HeaderVersion => "版本";
    public override string HeaderInput => "输入";
    public override string HeaderOutput => "输出";

    // Label
    public override string LabelFile => "文件";
    public override string LabelCompression => "压缩";
    public override string HeaderCompressionRatio => "压缩率";
    public override string LabelOutputFormat => "输出格式:";

    // Log
    public override string LogAnalyzeMetadata => "━━ 正在解析元数据... ━━";
    public override string LogCompressAndMerge => "正在压缩并合并...";
    public override string LogCompressAndSplit => "正在压缩并拆分...";
    public override string LogCompressionRatio => "压缩率";
    public override string LogDecompressAndMerge => "正在解压并合并";
    public override string LogDeleteIncompleteFile => "已删除未完整生成的文件。";
    public override string LogError(object arg0) => $"错误: {arg0}";
    public override string LogFinalId(object arg0, object arg1) => $"最终ID: {arg0} / 版本: {arg1}";
    public override string LogMergeComplete(object arg0) => $"合并完成: {arg0}";
    public override string LogMergeFailed(object? arg0, object arg1) => $"[{arg0}] 合并失败: {arg1}";
    public override string LogMerging => "正在合并...";
    public override string LogModeCompress => "压缩";
    public override string LogModeDecompress => "解压";
    public override string LogProcessError => "处理错误";
    public override string LogProcessStart => "开始";
    public override string LogSplitComplete(object arg0) => $"拆分完成: {arg0}";
    public override string LogSplitFailed(object arg0, object arg1) => $"拆分失败 (ID: {arg0}): {arg1}";
    public override string LogSplitPreparing(object arg0) => $"{arg0} 开始拆分";
    public override string LogSplitting => "正在拆分...";
    public override string LogStatusCompressing => "压缩中...";
    public override string LogStatusCopying => "复制中...";
    public override string LogStatusDecompressing => "解压中...";
    public override string LogStatusDone => "完成";
    public override string LogStatusValidating => "验证中...";
    public override string LogTitleGroupDetected(object arg0) => $"检测到 {arg0} 个标题组";
    public override string LogValidationComplete => "验证完成";
    public override string LogValidationStart => "开始验证";

    // Main
    public override string MainErrNoFiles => "请添加文件后重试。";
    public override string MainErrNoKeys => "未找到 prod.keys 文件。请将密钥文件放入应用程序文件夹中。";
    public override string MainErrNoOutput => "请设置输出文件夹。";
    public override string MainLogAllComplete(object arg0) => $"✓ 所有任务已完成。总耗时: {arg0}";
    public override string MainLogRecalculating => "正在重新分析文件类型...";
    public override string MainMsgDone => "完成！";
    public override string MainMsgSplitDone => "拆分完成。";

    // Menu
    public override string MenuOpenFolder => "打开文件夹";

    // Msg
    public override string MsgTitleError => "错误";
    public override string MsgTitleInfo => "信息";
    public override string MsgTitleQuestion => "确认";
    public override string MsgTitleWarning => "警告";

    // Status
    public override string StatusAnalyzing => "解析中...";
    public override string StatusNoKey => "无密钥";

    // ToolTip
    public override string ToolTipAddFile => "添加文件";
    public override string ToolTipAddFolder => "添加文件夹";
    public override string ToolTipBlocklessMode => "顺序访问（容量优先）";
    public override string ToolTipBlockMode => "随机访问（速度优先）";
    public override string ToolTipOpenWorkSpace => "打开输出文件夹";
    public override string ToolTipClear => "清空";
    public override string ToolTipRemoveSelected => "删除选中";
    public override string ToolTipValidateCompress => "压缩后执行额外验证";
    public override string ButtonAddFile => "添加文件";
    public override string ButtonAddFolder => "添加文件夹";
    public override string ButtonRemoveSelected => "删除选中";
    public override string ButtonClear => "清空";
    public override string MenuRemoveSelected => "删除选中";
    public override string MenuClear => "清空";
    public override string TabPatch => "补丁";
    public override string LabelPatch => "补丁";
    public override string TabSettings => "设置";
    public override string LabelSettings => "设置";
    public override string LabelVersion => "版本";
    public override string HintDropCueChd => "请拖放 CUE/CHD 文件";
    public override string HintDropSourceNsp => "请拖放原始 NSP/NSZ 文件";
    public override string HintDragOrSelectWorkFolder => "请拖入或选择工作文件夹";
    public override string HintDragOrSelectOutputFolder => "请拖入或选择输出文件夹";
    public override string HintDropImageFile => "请拖放图片文件";
    public override string HintDropFileOrFolder => "请将文件或文件夹拖放到此处";
    public override string HintDropGameFileOrFolder => "请将游戏文件/文件夹拖放到此处";
    public override string HintRepackReplaceRule => "本体/更新会自动替换，DLC 会继续添加";
    public override string HintSplitExpandRule => "合并 ROM 可通过 + 按钮展开，选择本体/更新/DLC 进行拆分";
    public override string LabelGameName => "游戏名称";
    public override string LabelPublisher => "发行商";
    public override string LabelGameId => "游戏 ID";
    public override string LabelNspTitleId => "NSP Title ID";
    public override string LabelWideScreen => "宽屏";
    public override string LabelSoundVolume => "音量:";
    public override string LabelLz4Compression => "Lz4 压缩";
    public override string LabelSupportedLanguages => "支持的语言";
    public override string LabelMultiRomIndex => "多 ROM 索引";
    public override string LabelMergeMode => "合并方式:";
    public override string LabelEstimated => "预计";
    public override string LabelActual => "实际";
    public override string LabelFileCount(object arg0) => $"{arg0} 个文件";
    public override string HeaderFileNameCompare => "文件名对比 (当前/更改后)";
    public override string DescConvertFormats => "在下列文件格式之间相互转换";
    public override string DescRenameByFormat => "按所选格式批量重命名文件";
    public override string ButtonForceLanguage => "强制设置语言";
    public override string ButtonCancelForceLanguage => "取消强制语言";
    public override string ButtonUnpack => "解包";
    public override string ButtonRepackFolderBased => "重新打包 (基于文件夹)";
    public override string ButtonUnpackAndRepackStreaming => "解包 + 重新打包 (基于流式处理)";
    public override string TabRepack => "重新打包";
    public override string TabMerge => "合并";
    public override string TabSplit => "拆分";
    public override string TabLz4Compression => "Lz4 压缩";
    public override string TabFormatConvert => "格式转换";
    public override string TabSaturnNspConvert => "Saturn NSP 转换";
    public override string TabIgnoreVersion => "忽略版本";
    public override string TabRename => "更改文件名";
    public override string RadioMergeGroupByTitleId => "按 Title ID 分组 (原有方式)";
    public override string RadioMergeAll => "全部";
    public override string MenuSelectFolder => "选择文件夹...";
    public override string MenuSelectArchive => "选择压缩文件 (zip/7z)...";
    public override string MenuDeselect => "取消选择";
    public override string MenuFindInFolder => "在文件夹中查找...";
    public override string MenuFindInArchive => "在压缩文件中查找 (zip/7z)...";
    public override string ToolTipTitleIdInput => "16 位十六进制数 (例如: 0100000000010000)。留空则保留原始 Title ID。";
    public override string ToolTipForceLanguage => "重新打包时，仅启用所选语言的标志，其余全部关闭。";
    public override string ToolTipMergeGroupByTitleId => "按 Title ID 对项目分组，并分别合并为独立文件。";
    public override string ToolTipMergeAll => "将所有项目合并为一个文件。";
    public override string ToolTipRomFsLz4 => "RomFS Lz4 压缩 (重新打包时应用)。本体和更新会合并为一个，因此勾选其中任意一个即会压缩。";
    public override string ToolTipBatchKoreanPatch => "批量指定韩文补丁 (根文件夹或压缩文件)";
    public override string ToolTipSelectAllToggle => "全选/取消全选";
    public override string ToolTipExpandCollapse => "展开/折叠";
    public override string HeaderSize => "大小";
    public override string ToolTipBrowseOutput => "选择输出文件夹";
    public override string HeaderConvert => "转换";
    public override string TabGeneral => "常规";
    public override string TabArcade => "街机";
    public override string TabConvert => "转换";
    public override string TabUtil => "工具";
    public override string ButtonReset => "重置";
    public override string ButtonCalculateHash => "计算哈希";
    public override string ButtonOk => "确定";
    public override string ToggleChangeOutputFolder => "更改输出文件夹";
    public override string ToggleAutoCompressAfterPatch => "打补丁后自动压缩";
    public override string ToggleAutoVersionDateInName => "在结果文件名中自动标注版本/日期";
    public override string MenuSelectFileIpsZip => "选择文件 (*.ips, *.zip)...";
    public override string MenuClearLog => "清空日志";
    public override string MenuCopyLog => "复制日志";
    public override string ToolTipSelectFile => "选择文件";
    public override string ToolTipRestoreDefaultPath => "恢复默认位置";
    public override string ToolTipChangePatchPackage => "更换为其他补丁包";
    public override string ToolTipChangePatchFile => "更换为其他补丁文件";
    public override string ToolTipAddSequentialPatch => "添加要连续应用的补丁";
    public override string ToolTipOpenGithubReleases => "打开 GitHub 发布页面";
    public override string ToolTipOpenHelp => "打开帮助页面";
    public override string LabelSource => "原始文件";
    public override string LabelSelectPatch => "选择补丁:";
    public override string LabelOthers => "其他";
    public override string LabelExpectedFileName => "预计文件名:";
    public override string LabelSupportedPatchFormats => "支持的补丁格式:";
    public override string LabelSourceAutoExtractFormats => "支持自动解压原始文件的格式:";
    public override string LabelFormatListIsoStandard => "ISO (标准), CUE+BIN, GDI+BIN+RAW, CCD+IMG+SUB";
    public override string LabelFormatList3dsCciCia => "3DS (与 CCI 格式相同), CCI, CIA";
    public override string LabelFormat3dsCci => "3DS (与 CCI 格式相同)";
    public override string LabelFormatChdIsoBased => "CHD (基于 ISO)";
    public override string LabelFormatBinCueSingleTrack => "BIN+CUE (单轨)";
    public override string DescAutoCompressByRule => "将根据下列规则自动选择格式并进行压缩或解压";
    public override string DescConvertByRule => "按下列规则进行压缩、解压和格式转换";
    public override string DescAutoMatchByExtension => "将按扩展名自动匹配";
    public override string DescAutoVersionDateInName => "从补丁文件名中读取版本和日期并显示在结果文件名中";
    public override string DescPc98Hdi => "按逻辑路径查找并替换 HDI (FAT12/16) 内部文件。挂载路径会自动检测。";
    public override string DescArchivePasswordProtected => "此压缩文件受密码保护。";
    public override string HintDropPatchFile => "请拖放补丁文件";
    public override string HintDropArcadeFiles => "请将原始 ZIP 和补丁 (IPS, 文件夹, ZIP) 拖放到上方";
    public override string StatusMatched => "已匹配";
    public override string StatusUnmatched => "未匹配";
    public override string StatusHashCancelledByPatch => "因开始打补丁，哈希计算已取消";
    public override string StatusHashFailed => "哈希计算失败";
    public override string StatusWrongPassword => "密码不正确。";
    public override string TitleArchivePassword => "压缩文件密码";
}