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
    public override string LabelCompressionRatio => "压缩率";
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
    public override string ToolTipRemoveAll => "清空";
    public override string ToolTipRemoveSelected => "删除选中";
    public override string ToolTipValidateCompress => "压缩后执行额外验证";
}