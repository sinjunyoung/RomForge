namespace Common.Localization.Languages;

public sealed class Japanese : LocalizedStrings
{
    public override string Code => "ja-JP";
    public override string DisplayName => "日本語";
    public override string EnglishName => "Japanese";

    // Button
    public override string ButtonCancel => "キャンセル";
    public override string ButtonSettings => "設定";
    public override string ButtonStart => "開始";

    // Dialog
    public override string DialogSelectGameFile => "ゲームファイルを選択";

    // Error
    public override string ErrorNoMetadata => "解析可能なメタデータがありません。";

    // Filter
    public override string FilterAllFiles => "すべてのファイル";
    public override string FilterSwitchFiles => "Nintendo Switchファイル";

    // Hint
    public override string HintSelectOutput => "出力フォルダーを選択してください";

    // Header
    public override string HeaderFileName => "ファイル名";
    public override string HeaderFilePath => "パス";
    public override string HeaderTitle => "タイトル";
    public override string HeaderPatch => "パッチ";
    public override string HeaderVersion => "バージョン";
    public override string HeaderInput => "入力";
    public override string HeaderOutput => "出力";

    // Label
    public override string LabelFile => "ファイル";
    public override string LabelCompression => "圧縮";
    public override string LabelCompressionRatio => "圧縮率";
    public override string LabelOutputFormat => "出力形式:";

    // Log
    public override string LogAnalyzeMetadata => "━━ メタデータ解析中... ━━";
    public override string LogCompressAndMerge => "圧縮してマージ中...";
    public override string LogCompressAndSplit => "圧縮して分割中...";
    public override string LogCompressionRatio => "圧縮率";
    public override string LogDecompressAndMerge => "解凍およびマージ中";
    public override string LogDeleteIncompleteFile => "不完全に生成されたファイルを削除しました。";
    public override string LogError(object arg0) => $"エラー: {arg0}";
    public override string LogFinalId(object arg0, object arg1) => $"最終ID: {arg0} / バージョン: {arg1}";
    public override string LogMergeComplete(object arg0) => $"マージ完了: {arg0}";
    public override string LogMergeFailed(object? arg0, object arg1) => $"[{arg0}] マージ失敗: {arg1}";
    public override string LogMerging => "マージ中...";
    public override string LogModeCompress => "圧縮";
    public override string LogModeDecompress => "解凍";
    public override string LogProcessError => "処理エラー";
    public override string LogProcessStart => "開始";
    public override string LogSplitComplete(object arg0) => $"分割完了: {arg0}";
    public override string LogSplitFailed(object arg0, object arg1) => $"分割失敗 (ID: {arg0}): {arg1}";
    public override string LogSplitPreparing(object arg0) => $"{arg0} 分割を開始";
    public override string LogSplitting => "分割中...";
    public override string LogStatusCompressing => "圧縮中...";
    public override string LogStatusCopying => "コピー中...";
    public override string LogStatusDecompressing => "解凍中...";
    public override string LogStatusDone => "完了";
    public override string LogStatusValidating => "検証中...";
    public override string LogTitleGroupDetected(object arg0) => $"{arg0}個のタイトルグループを検出";
    public override string LogValidationComplete => "検証完了";
    public override string LogValidationStart => "検証開始";

    // Main
    public override string MainErrNoFiles => "ファイルを追加して再試行してください。";
    public override string MainErrNoKeys => "prod.keys ファイルが見つかりません。キー ファイルをアプリケーション フォルダーに配置してください。";
    public override string MainErrNoOutput => "出力フォルダーを設定してください。";
    public override string MainLogAllComplete(object arg0) => $"✓ すべてのタスクが完了しました。合計時間: {arg0}";
    public override string MainLogRecalculating => "ファイルタイプを再分析中...";
    public override string MainMsgDone => "完了しました！";
    public override string MainMsgSplitDone => "分割が完了しました。";

    // Menu
    public override string MenuOpenFolder => "フォルダーを開く";

    // Msg
    public override string MsgTitleError => "エラー";
    public override string MsgTitleInfo => "情報";
    public override string MsgTitleQuestion => "確認";
    public override string MsgTitleWarning => "警告";

    // Status
    public override string StatusAnalyzing => "解析中...";
    public override string StatusNoKey => "キーなし";

    // ToolTip
    public override string ToolTipAddFile => "ファイルを追加";
    public override string ToolTipAddFolder => "フォルダーを追加";
    public override string ToolTipBlocklessMode => "シーケンシャルアクセス（容量優先）";
    public override string ToolTipBlockMode => "ランダムアクセス（速度優先）";
    public override string ToolTipOpenWorkSpace => "出力フォルダーを開く";
    public override string ToolTipRemoveAll => "クリア";
    public override string ToolTipRemoveSelected => "選択削除";
    public override string ToolTipValidateCompress => "圧縮後に追加の検証を実行します";
}