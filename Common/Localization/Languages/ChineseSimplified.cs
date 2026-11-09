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
    public override string HintSelectOutput => "请选择输出文件夹。";

    // Header
    public override string HeaderFileName => "文件名";
    public override string HeaderFilePath => "路径";

    // Label
    public override string LabelCompression => "压缩";
    public override string LabelCompressionRatio => "压缩率";

    // Log
    public override string LogAnalyzeMetadata => "━━ 正在解析元数据... ━━";
    public override string LogBaseMissingSkip(object arg0) => $"[{arg0}] 缺少 Base（本体）- 已跳过";
    public override string LogCompressAndMerge => "正在压缩并合并...";
    public override string LogCompressAndSplit => "正在压缩并分割...";
    public override string LogCompressionRatio => "压缩率";
    public override string LogDecompressAndMerge => "正在解压并合并";
    public override string LogDecompressAndSplit => "正在解压并分割";
    public override string LogDeleteIncompleteFile => "已删除未完整生成的文件。";
    public override string LogError(object arg0) => $"错误: {arg0}";
    public override string LogFinalId(object arg0, object arg1) => $"最终ID: {arg0} / 版本: {arg1}";
    public override string LogMergeComplete(object arg0) => $"合并完成: {arg0}";
    public override string LogMergeFailed(object? arg0, object arg1) => $"[{arg0}] 合并失败：{arg1}";
    public override string LogMergeStart => "━━ 开始合并 Super NSP ━━";
    public override string LogMerging => "正在合并...";
    public override string LogModeCompress => "压缩";
    public override string LogModeDecompress => "解压";
    public override string LogProcessError => "处理错误";
    public override string LogProcessStart => "开始";
    public override string LogSplitComplete(object arg0) => $"拆分完成: {arg0}";
    public override string LogSplitDecompressing(object arg0) => $"- {arg0} 正在解压并分割...";
    public override string LogSplitExtracting(object arg0) => $"- {arg0} 正在分割...";
    public override string LogSplitFailed(object arg0, object arg1) => $"拆分失败 (ID: {arg0}): {arg1}";
    public override string LogSplitNoTarget => "已是单一内容或没有可拆分的目标。";
    public override string LogSplitPreparing(object arg0) => $"{arg0} 开始分割";
    public override string LogSplitting => "正在分离...";
    public override string LogStatusCompressing => "压缩中...";
    public override string LogStatusCopying => "复制中...";
    public override string LogStatusDecompressing => "解压中...";
    public override string LogStatusDone => "完成";
    public override string LogStatusValidating => "校验中...";
    public override string LogTitleGroupDetected(object arg0) => $"检测到 {arg0} 个标题组";
    public override string LogValidating => "校验中...";
    public override string LogValidationComplete => "验证完成";
    public override string LogValidationStart => "开始验证";
    public override string LogWriteNspFailed(object arg0) => $"写入 NSP 失败: {arg0}";

    // Main
    public override string MainCompressTitle => "NSW 压缩工具";
    public override string MainErrNoBase => "请添加包含基础游戏的文件。";
    public override string MainErrNoFiles => "请添加文件后重试。";
    public override string MainErrNoKeys => "未找到 prod.keys 文件。请将密钥文件放入应用程序文件夹中。";
    public override string MainErrNoKeysAndroid => "未找到 prod.keys 文件。请将密钥文件放入 Download 文件夹中。";
    public override string MainErrNoOutput => "请设置输出文件夹。";
    public override string MainLogAllComplete(object arg0) => $"✓ 所有任务已完成。总耗时: {arg0}";
    public override string MainLogAllSplitDone(object arg0) => $"✓ 所有拆分任务已完成！总耗时: {arg0}";
    public override string MainLogElapsed(object arg0) => $"耗时: {arg0}";
    public override string MainLogRecalculating => "正在重新分析文件类型...";
    public override string MainLogSplitStart(object arg0) => $"━━ 开始拆分解析: {arg0} ━━";
    public override string MainMsgDone => "完成！";
    public override string MainMsgSplitDone => "拆分完成。";
    public override string MainSelectFolder => "选择输出文件夹";
    public override string MainTitle => "NSW 合并工具";

    // Menu
    public override string MenuOpenFolder => "打开文件夹";

    // Msg
    public override string MsgTitleError => "错误";
    public override string MsgTitleInfo => "信息";
    public override string MsgTitleQuestion => "确认";
    public override string MsgTitleWarning => "警告";

    // Settings
    public override string SettingsCompressMode => "压缩模式";
    public override string SettingsForceKeyGen0 => "强制 KeyGen 0";
    public override string SettingsTabGeneral => "常规";

    // Status
    public override string StatusAnalyzing => "解析中...";
    public override string StatusNoKey => "无密钥";
    public override string StatusProcessing => "处理中...";

    // ToolTip
    public override string ToolTipAddFile => "加文件";
    public override string ToolTipAddFolder => "加文件夹";
    public override string ToolTipBlocklessMode => "顺序访问（容量优先）";
    public override string ToolTipBlockMode => "随机访问（速度优先）";
    public override string ToolTipBrowseOutput => "浏览输出文件夹";
    public override string ToolTipCompress => "勾选时压缩为NSZ（仅合并）";
    public override string ToolTipCompressLevel => "3：最快 | 9：平衡 | 18：推荐";
    public override string ToolTipForceKeyGen0 => "将固件KeyGen强制为0";
    public override string ToolTipOpenWorkSpace => "打开输出文件夹";
    public override string ToolTipRemoveAll => "清空";
    public override string ToolTipRemoveSelected => "删除选中";
    public override string ToolTipValidateCompress => "压缩后执行额外校验";
}