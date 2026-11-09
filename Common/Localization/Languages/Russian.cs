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
    public override string LabelCompressionRatio => "Коэффициент сжатия";
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
    public override string ToolTipRemoveAll => "Очистить";
    public override string ToolTipRemoveSelected => "Удалить";
    public override string ToolTipValidateCompress => "Выполняет дополнительную проверку после сжатия";
}