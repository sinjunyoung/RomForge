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
    public override string HeaderCompressionRatio => "圧縮率";
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
    public override string ToolTipClear => "クリア";
    public override string ToolTipRemoveSelected => "選択削除";
    public override string ToolTipValidateCompress => "圧縮後に追加の検証を実行します";
    public override string ButtonAddFile => "ファイルを追加";
    public override string ButtonAddFolder => "フォルダーを追加";
    public override string ButtonRemoveSelected => "選択削除";
    public override string ButtonClear => "クリア";
    public override string MenuRemoveSelected => "選択削除";
    public override string MenuClear => "クリア";
    public override string TabPatch => "パッチ";
    public override string LabelPatch => "パッチ";
    public override string TabSettings => "設定";
    public override string LabelSettings => "設定";
    public override string LabelVersion => "バージョン";
    public override string HintDropCueChd => "CUE/CHDファイルをドラッグ&ドロップしてください";
    public override string HintDropSourceNsp => "元のNSP/NSZファイルをドラッグ&ドロップしてください";
    public override string HintDragOrSelectWorkFolder => "作業フォルダーをドラッグするか選択してください";
    public override string HintDragOrSelectOutputFolder => "出力フォルダーをドラッグするか選択してください";
    public override string HintDropImageFile => "画像ファイルをドラッグ&ドロップしてください";
    public override string HintDropFileOrFolder => "ファイルまたはフォルダーをここにドラッグ&ドロップしてください";
    public override string HintDropGameFileOrFolder => "ゲームファイル/フォルダーをここにドラッグ&ドロップしてください";
    public override string HintRepackReplaceRule => "本編/アップデートは自動的に置き換えられ、DLCは追加されます";
    public override string HintSplitExpandRule => "統合ROMは+ボタンで展開して、本編/アップデート/DLCを選んで分割できます";
    public override string LabelGameName => "ゲーム名";
    public override string LabelPublisher => "パブリッシャー";
    public override string LabelGameId => "ゲームID";
    public override string LabelNspTitleId => "NSPタイトルID";
    public override string LabelWideScreen => "ワイドスクリーン";
    public override string LabelSoundVolume => "サウンド音量:";
    public override string LabelLz4Compression => "Lz4圧縮";
    public override string LabelSupportedLanguages => "対応言語";
    public override string LabelMultiRomIndex => "マルチROMインデックス";
    public override string LabelMergeMode => "マージ方式:";
    public override string LabelEstimated => "予想";
    public override string LabelActual => "実際";
    public override string LabelFileCount(object arg0) => $"ファイル {arg0}個";
    public override string HeaderFileNameCompare => "ファイル名の比較 (現在/変更後)";
    public override string DescConvertFormats => "下記のファイルフォーマットを相互に変換します";
    public override string DescRenameByFormat => "選択したフォーマットに合わせてファイル名を一括変更します";
    public override string ButtonForceLanguage => "言語を強制設定";
    public override string ButtonCancelForceLanguage => "強制言語を解除";
    public override string ButtonUnpack => "アンパック";
    public override string ButtonRepackFolderBased => "リパック (フォルダー基準)";
    public override string ButtonUnpackAndRepackStreaming => "アンパック + リパック (ストリーミング基準)";
    public override string TabRepack => "リパック";
    public override string TabMerge => "マージ";
    public override string TabSplit => "分割";
    public override string TabLz4Compression => "Lz4圧縮";
    public override string TabFormatConvert => "フォーマット変換";
    public override string TabSaturnNspConvert => "Saturn NSP変換";
    public override string TabIgnoreVersion => "バージョン無視";
    public override string TabRename => "ファイル名変更";
    public override string RadioMergeGroupByTitleId => "Title ID別にグループ化 (従来方式)";
    public override string RadioMergeAll => "すべて";
    public override string MenuSelectFolder => "フォルダーを選択...";
    public override string MenuSelectArchive => "圧縮ファイルを選択 (zip/7z)...";
    public override string MenuDeselect => "選択解除";
    public override string MenuFindInFolder => "フォルダーから検索...";
    public override string MenuFindInArchive => "圧縮ファイルから検索 (zip/7z)...";
    public override string ToolTipTitleIdInput => "16桁の16進数 (例: 0100000000010000)。空欄の場合は元のTitle IDを維持します。";
    public override string ToolTipForceLanguage => "リパック時、選択した言語のみフラグを有効にし、それ以外はすべて無効にします。";
    public override string ToolTipMergeGroupByTitleId => "Title IDごとに項目をグループ化し、それぞれ別のファイルにマージします。";
    public override string ToolTipMergeAll => "すべての項目を1つのファイルにマージします。";
    public override string ToolTipRomFsLz4 => "RomFS Lz4圧縮 (リパック時に適用)。本編とアップデートは1つに統合されるため、どちらか一方にチェックを入れると圧縮されます。";
    public override string ToolTipBatchKoreanPatch => "韓国語パッチを一括指定 (ルートフォルダーまたは圧縮ファイル)";
    public override string ToolTipSelectAllToggle => "すべて選択/解除";
    public override string ToolTipExpandCollapse => "展開/折りたたみ";
    public override string HeaderSize => "サイズ";
    public override string ToolTipBrowseOutput => "出力フォルダーを選択";
    public override string HeaderConvert => "変換";
    public override string TabGeneral => "一般";
    public override string TabArcade => "アーケード";
    public override string TabConvert => "変換";
    public override string TabUtil => "ユーティリティ";
    public override string ButtonReset => "リセット";
    public override string ButtonCalculateHash => "ハッシュを計算";
    public override string ButtonOk => "OK";
    public override string ToggleChangeOutputFolder => "出力フォルダーを変更";
    public override string ToggleAutoCompressAfterPatch => "パッチ後に自動圧縮";
    public override string ToggleAutoVersionDateInName => "結果ファイル名にバージョン/日付を自動表記";
    public override string MenuSelectFileIpsZip => "ファイルを選択 (*.ips, *.zip)...";
    public override string MenuClearLog => "ログをクリア";
    public override string MenuCopyLog => "ログをコピー";
    public override string ToolTipSelectFile => "ファイルを選択";
    public override string ToolTipRestoreDefaultPath => "既定の場所に戻す";
    public override string ToolTipChangePatchPackage => "別のパッチパッケージに変更";
    public override string ToolTipChangePatchFile => "別のパッチファイルに変更";
    public override string ToolTipAddSequentialPatch => "連続して適用するパッチを追加";
    public override string ToolTipOpenGithubReleases => "GitHubのリリースページを開きます";
    public override string ToolTipOpenHelp => "ヘルプページを開きます";
    public override string LabelSource => "元ファイル";
    public override string LabelSelectPatch => "パッチを選択:";
    public override string LabelOthers => "その他";
    public override string LabelExpectedFileName => "予想ファイル名:";
    public override string LabelSupportedPatchFormats => "対応パッチフォーマット:";
    public override string LabelSourceAutoExtractFormats => "元ファイルの自動解凍に対応するフォーマット:";
    public override string LabelFormatListIsoStandard => "ISO (標準), CUE+BIN, GDI+BIN+RAW, CCD+IMG+SUB";
    public override string LabelFormatList3dsCciCia => "3DS (CCIと同じフォーマット), CCI, CIA";
    public override string LabelFormat3dsCci => "3DS (CCIと同じフォーマット)";
    public override string LabelFormatChdIsoBased => "CHD (ISOベース)";
    public override string LabelFormatBinCueSingleTrack => "BIN+CUE (シングルトラック)";
    public override string DescAutoCompressByRule => "下記の基準に従ってフォーマットが自動選択され、圧縮または解凍されます";
    public override string DescConvertByRule => "下記の基準で圧縮、解凍、フォーマット変換を行います";
    public override string DescAutoMatchByExtension => "拡張子に基づいて自動的にマッチングされます";
    public override string DescAutoVersionDateInName => "パッチファイル名からバージョンと日付を読み取り、結果ファイル名に表示します";
    public override string DescPc98Hdi => "HDI (FAT12/16) 内のファイルを論理パスで検索して置き換えます。マウントパスは自動的に検出されます。";
    public override string DescArchivePasswordProtected => "この圧縮ファイルはパスワードで保護されています。";
    public override string HintDropPatchFile => "パッチファイルをドラッグ&ドロップしてください";
    public override string HintDropArcadeFiles => "元のZIPとパッチ (IPS, フォルダー, ZIP) を上にドラッグ&ドロップしてください";
    public override string StatusMatched => "マッチ";
    public override string StatusUnmatched => "未マッチ";
    public override string StatusHashCancelledByPatch => "パッチの開始によりハッシュ計算がキャンセルされました";
    public override string StatusHashFailed => "ハッシュ計算に失敗しました";
    public override string StatusWrongPassword => "パスワードが正しくありません。";
    public override string TitleArchivePassword => "圧縮ファイルのパスワード";
}