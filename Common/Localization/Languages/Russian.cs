namespace Common.Localization.Languages;

public sealed class Russian : LocalizedStrings
{
    public override string Code => "ru-RU";
    public override string DisplayName => "Русский";
    public override string EnglishName => "Russian";

    // Button
    public override string ButtonCancel => "Отмена";
    public override string ButtonSettings => "Настройки";
    public override string ButtonStart => "Начать";

    // Dialog
    public override string DialogSelectGameFile => "Выберите файл игры";

    // Error
    public override string ErrorNoMetadata => "Не найдено метаданных для анализа.";

    // Filter
    public override string FilterAllFiles => "Все файлы";
    public override string FilterSwitchFiles => "Файлы Switch";

    // Hint
    public override string HintSelectOutput => "Выберите папку вывода";

    // Header
    public override string HeaderFileName => "Имя файла";
    public override string HeaderFilePath => "Путь";
    public override string HeaderTitle => "Название";
    public override string HeaderPatch => "Патч";
    public override string HeaderVersion => "Версия";
    public override string HeaderInput => "Ввод";
    public override string HeaderOutput => "Вывод";

    // Label
    public override string LabelFile => "Файл";
    public override string LabelCompression => "Сжатие";
    public override string HeaderCompressionRatio => "Коэффициент сжатия";
    public override string LabelOutputFormat => "Формат вывода:";

    // Log
    public override string LogAnalyzeMetadata => "━━ Анализ метаданных... ━━";
    public override string LogCompressAndMerge => "Сжатие и объединение...";
    public override string LogCompressAndSplit => "Сжатие и разделение...";
    public override string LogCompressionRatio => "Коэффициент сжатия";
    public override string LogDecompressAndMerge => "Распаковка и объединение";
    public override string LogDeleteIncompleteFile => "Неполностью созданный файл удалён.";
    public override string LogError(object arg0) => $"Произошла ошибка: {arg0}";
    public override string LogFinalId(object arg0, object arg1) => $"Итоговый ID: {arg0} / Версия: {arg1}";
    public override string LogMergeComplete(object arg0) => $"Объединение завершено: {arg0}";
    public override string LogMergeFailed(object? arg0, object arg1) => $"[{arg0}] Ошибка объединения: {arg1}";
    public override string LogMerging => "Идёт объединение...";
    public override string LogModeCompress => "Сжатие";
    public override string LogModeDecompress => "Распаковка";
    public override string LogProcessError => "Ошибка обработки";
    public override string LogProcessStart => "Начало";
    public override string LogSplitComplete(object arg0) => $"Разделение завершено: {arg0}";
    public override string LogSplitFailed(object arg0, object arg1) => $"Ошибка разделения (ID: {arg0}): {arg1}";
    public override string LogSplitPreparing(object arg0) => $"{arg0} Начало разделения";
    public override string LogSplitting => "Идёт разделение...";
    public override string LogStatusCompressing => "Сжатие...";
    public override string LogStatusCopying => "Копирование...";
    public override string LogStatusDecompressing => "Распаковка...";
    public override string LogStatusDone => "Готово";
    public override string LogStatusValidating => "Проверка...";
    public override string LogTitleGroupDetected(object arg0) => $"Обнаружено групп игр: {arg0}";
    public override string LogValidationComplete => "Проверка завершена";
    public override string LogValidationStart => "Начало проверки";

    // Main
    public override string MainErrNoFiles => "Добавьте файлы и попробуйте снова.";
    public override string MainErrNoKeys => "Файл prod.keys не найден. Поместите файл ключей в папку приложения.";
    public override string MainErrNoOutput => "Укажите папку вывода.";
    public override string MainLogAllComplete(object arg0) => $"✓ Все задачи завершены. Общее время: {arg0}";
    public override string MainLogRecalculating => "Повторный анализ типов файлов...";
    public override string MainMsgDone => "Готово!";
    public override string MainMsgSplitDone => "Разделение завершено.";

    // Menu
    public override string MenuOpenFolder => "Открыть папку";

    // Msg
    public override string MsgTitleError => "Ошибка";
    public override string MsgTitleInfo => "Информация";
    public override string MsgTitleQuestion => "Подтверждение";
    public override string MsgTitleWarning => "Предупреждение";

    // Status
    public override string StatusAnalyzing => "Анализ...";
    public override string StatusNoKey => "Нет ключа";

    // ToolTip
    public override string ToolTipAddFile => "Добавить файл";
    public override string ToolTipAddFolder => "Добавить папку";
    public override string ToolTipBlocklessMode => "Последовательный доступ (приоритет объёма)";
    public override string ToolTipBlockMode => "Случайный доступ (приоритет скорости)";
    public override string ToolTipOpenWorkSpace => "Открыть папку вывода";
    public override string ToolTipClear => "Очистить";
    public override string ToolTipRemoveSelected => "Удалить";
    public override string ToolTipValidateCompress => "Выполняет дополнительную проверку после сжатия";
    public override string ButtonAddFile => "Добавить файл";
    public override string ButtonAddFolder => "Добавить папку";
    public override string ButtonRemoveSelected => "Удалить";
    public override string ButtonClear => "Очистить";
    public override string MenuRemoveSelected => "Удалить";
    public override string MenuClear => "Очистить";
    public override string TabPatch => "Патч";
    public override string LabelPatch => "Патч";
    public override string TabSettings => "Настройки";
    public override string LabelSettings => "Настройки";
    public override string LabelVersion => "Версия";
    public override string HintDropCueChd => "Перетащите файл CUE/CHD";
    public override string HintDropSourceNsp => "Перетащите исходный файл NSP/NSZ";
    public override string HintDragOrSelectWorkFolder => "Перетащите или выберите рабочую папку";
    public override string HintDragOrSelectOutputFolder => "Перетащите или выберите папку вывода";
    public override string HintDropImageFile => "Перетащите файл изображения";
    public override string HintDropFileOrFolder => "Перетащите файлы или папки сюда";
    public override string HintDropGameFileOrFolder => "Перетащите файлы/папки игры сюда";
    public override string HintRepackReplaceRule => "Игра и обновление заменяются автоматически, DLC добавляются";
    public override string HintSplitExpandRule => "Разверните объединённый ROM кнопкой +, чтобы выбрать игру, обновление или DLC для разделения";
    public override string LabelGameName => "Название игры";
    public override string LabelPublisher => "Издатель";
    public override string LabelGameId => "ID игры";
    public override string LabelNspTitleId => "Title ID NSP";
    public override string LabelWideScreen => "Широкий экран";
    public override string LabelSoundVolume => "Громкость звука:";
    public override string LabelLz4Compression => "Сжатие Lz4";
    public override string LabelSupportedLanguages => "Поддерживаемые языки";
    public override string LabelMultiRomIndex => "Индекс мульти-ROM";
    public override string LabelMergeMode => "Режим объединения:";
    public override string LabelEstimated => "Ожидаемо";
    public override string LabelActual => "Фактически";
    public override string LabelFileCount(object arg0) => $"Файлов: {arg0}";
    public override string HeaderFileNameCompare => "Сравнение имён файлов (текущее/новое)";
    public override string DescConvertFormats => "Преобразование между перечисленными ниже форматами файлов";
    public override string DescRenameByFormat => "Пакетное переименование файлов по выбранному формату";
    public override string ButtonForceLanguage => "Принудительный язык";
    public override string ButtonCancelForceLanguage => "Отменить принудительный язык";
    public override string ButtonUnpack => "Распаковка";
    public override string ButtonRepackFolderBased => "Перепаковка (на основе папки)";
    public override string ButtonUnpackAndRepackStreaming => "Распаковка + перепаковка (потоковая)";
    public override string TabRepack => "Перепаковка";
    public override string TabMerge => "Объединение";
    public override string TabSplit => "Разделение";
    public override string TabLz4Compression => "Сжатие Lz4";
    public override string TabFormatConvert => "Преобразование формата";
    public override string TabSaturnNspConvert => "Преобразование Saturn NSP";
    public override string TabIgnoreVersion => "Игнорировать версию";
    public override string TabRename => "Переименование файлов";
    public override string RadioMergeGroupByTitleId => "Группировка по Title ID (прежний способ)";
    public override string RadioMergeAll => "Все";
    public override string MenuSelectFolder => "Выбрать папку...";
    public override string MenuSelectArchive => "Выбрать архив (zip/7z)...";
    public override string MenuDeselect => "Отменить выбор";
    public override string MenuFindInFolder => "Найти в папке...";
    public override string MenuFindInArchive => "Найти в архиве (zip/7z)...";
    public override string ToolTipTitleIdInput => "16-значное шестнадцатеричное число (например: 0100000000010000). Оставьте пустым, чтобы сохранить исходный Title ID.";
    public override string ToolTipForceLanguage => "При перепаковке включаются только выбранные языки, остальные отключаются.";
    public override string ToolTipMergeGroupByTitleId => "Группирует элементы по Title ID и объединяет каждую группу в отдельный файл.";
    public override string ToolTipMergeAll => "Объединяет все элементы в один файл.";
    public override string ToolTipRomFsLz4 => "Сжатие RomFS в Lz4 (применяется при перепаковке). Игра и обновление объединяются в одно, поэтому сжатие включается, если отмечено любое из них.";
    public override string ToolTipBatchKoreanPatch => "Массовое назначение корейских патчей (корневая папка или архив)";
    public override string ToolTipSelectAllToggle => "Выбрать/снять выбор со всех";
    public override string ToolTipExpandCollapse => "Развернуть/Свернуть";
    public override string HeaderSize => "Размер";
    public override string ToolTipBrowseOutput => "Выбрать папку вывода";
}