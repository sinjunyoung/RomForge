namespace Common.Localization.Languages;

public sealed class Japanese : LocalizedStrings
{
    public override string Code => "ja-JP";
    public override string DisplayName => "日本語";

    // Button
    public override string ButtonCancel => "キャンセル";
    public override string ButtonSettings => "設定";
    public override string ButtonStart => "開始";

    // Dialog
    public override string DialogSelectGameFile => "ゲームファイルを選択";

    // Error
    public override string ErrorNoCompressFiles => "圧縮するファイルがありません。";
    public override string ErrorNoDecompressFiles => "解凍するファイルがありません。";
    public override string ErrorNoMetadata => "解析可能なメタデータがありません。";

    // Filter
    public override string FilterAllFiles => "すべてのファイル";
    public override string FilterSwitchFiles => "Nintendo Switchファイル";

    // Hint
    public override string HintDragAndDrop => "ここにファイルをドラッグ＆ドロップしてください（NSP, NSZ, XCI, XCZ）";
    public override string HintSelectOutput => "出力フォルダーを選択してください。";

    // Header
    public override string HeaderExt => "拡張子";
    public override string HeaderFileName => "ファイル名";
    public override string HeaderFilePath => "パス";
    public override string HeaderSize => "サイズ";
    public override string HeaderType => "種類";

    // Label
    public override string LabelCompression => "圧縮";
    public override string LabelOutputPath => "出力フォルダー";

    // Log
    public override string LogAnalyzeMetadata => "━━ メタデータ解析中 ━━";
    public override string LogBaseMissingSkip(object arg0) => $"[{arg0}] Base（本編）がありません - スキップ";
    public override string LogCompressAndMerge => "圧縮してマージ中...";
    public override string LogCompressAndSplit => "圧縮して分割中...";
    public override string LogCompressionRatio => "圧縮率";
    public override string LogDecompressAndMerge => "解凍およびマージ中";
    public override string LogDecompressAndSplit => "解凍および分割中";
    public override string LogDeleteIncompleteFile => "不完全に生成されたファイルを削除しました。";
    public override string LogError(object arg0) => $"エラー: {arg0}";
    public override string LogFinalId(object arg0, object arg1) => $"最終ID: {arg0} / バージョン: {arg1}";
    public override string LogMergeComplete(object arg0) => $"マージ完了: {arg0}";
    public override string LogMergeFailed(object? arg0, object arg1) => $"[{arg0}] マージ失敗: {arg1}";
    public override string LogMergeStart => "━━ Super NSP マージを開始します ━━";
    public override string LogMerging => "マージ中...";
    public override string LogModeCompress => "圧縮";
    public override string LogModeDecompress => "解凍";
    public override string LogProcessError => "処理エラー";
    public override string LogProcessStart => "開始";
    public override string LogSplitComplete(object arg0) => $"分割完了: {arg0}";
    public override string LogSplitDecompressing(object arg0) => $"- {arg0} 解凍して分割中...";
    public override string LogSplitExtracting(object arg0) => $"- {arg0} 分割中...";
    public override string LogSplitFailed(object arg0, object arg1) => $"分割失敗 (ID: {arg0}): {arg1}";
    public override string LogSplitNoTarget => "すでに単一コンテンツ、または分割対象がありません。";
    public override string LogSplitPreparing(object arg0) => $"{arg0} 分割を開始";
    public override string LogSplitting => "分離中...";
    public override string LogStatusCompressing => "圧縮中...";
    public override string LogStatusCopying => "コピー中...";
    public override string LogStatusDecompressing => "解凍中...";
    public override string LogStatusDone => "完了";
    public override string LogStatusValidating => "検証中...";
    public override string LogTitleGroupDetected(object arg0) => $"{arg0}個のタイトルグループを検出";
    public override string LogValidating => "検証中...";
    public override string LogValidationComplete => "検証完了";
    public override string LogValidationStart => "検証開始";
    public override string LogWriteNspFailed(object arg0) => $"NSP書き込み失敗: {arg0}";

    // Main
    public override string MainCompressTitle => "NSW圧縮ツール";
    public override string MainErrNoBase => "ベースゲームを含むファイルを追加してください。";
    public override string MainErrNoFiles => "ファイルを追加して再試行してください。";
    public override string MainErrNoKeys => "prod.keys ファイルが見つかりません。キー ファイルをアプリケーション フォルダーに配置してください。";
    public override string MainErrNoKeysAndroid => "prod.keys ファイルが見つかりません。キー ファイルを Download フォルダーに配置してください。";
    public override string MainErrNoOutput => "出力フォルダーを設定してください。";
    public override string MainLogAllComplete(object arg0) => $"✓ すべてのタスクが完了しました。合計時間: {arg0}";
    public override string MainLogAllSplitDone(object arg0) => $"✓ すべての分割処理が完了しました！合計時間: {arg0}";
    public override string MainLogElapsed(object arg0) => $"経過時間: {arg0}";
    public override string MainLogRecalculating => "ファイルタイプを再分析中...";
    public override string MainLogSplitStart(object arg0) => $"━━ 分割解析を開始: {arg0} ━━";
    public override string MainMsgDone => "完了しました！";
    public override string MainMsgSplitDone => "分割が完了しました。";
    public override string MainSelectFolder => "出力フォルダーを選択";
    public override string MainTitle => "NSW Merge Tool";

    // Menu
    public override string MenuOpenFolder => "フォルダーを開く";

    // Msg
    public override string MsgTitleError => "エラー";
    public override string MsgTitleInfo => "情報";
    public override string MsgTitleQuestion => "確認";
    public override string MsgTitleWarning => "警告";

    // Settings
    public override string SettingsCompressMode => "圧縮モード";
    public override string SettingsForceKeyGen0 => "KeyGen 0を強制";
    public override string SettingsTabGeneral => "一般";

    // Status
    public override string StatusAnalyzing => "解析中...";
    public override string StatusNoKey => "キーなし";
    public override string StatusProcessing => "処理中...";

    // ToolTip
    public override string ToolTipAddFile => "ファイルを追加";
    public override string ToolTipBlocklessMode => "シーケンシャルアクセス（容量優先）";
    public override string ToolTipBlockMode => "ランダムアクセス（速度優先）";
    public override string ToolTipBrowseOutput => "出力フォルダーを選択します";
    public override string ToolTipCompress => "チェック時にNSZへ圧縮します（結合のみ）";
    public override string ToolTipCompressLevel => "3: 最速 | 9: バランス | 18: 推奨";
    public override string ToolTipForceKeyGen0 => "ファームウェアKeyGenを0に固定";
    public override string ToolTipOpenWorkSpace => "出力フォルダーを開きます";
    public override string ToolTipRemoveAll => "すべて削除";
    public override string ToolTipRemoveSelected => "選択項目を削除";
    public override string ToolTipValidateCompress => "圧縮後に追加の検証を実行します";
}