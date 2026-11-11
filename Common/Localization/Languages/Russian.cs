namespace Common.Localization.Languages;

public sealed class Russian : LocalizedStrings
{
    // Language
    public override string Code => "ru-RU";
    public override string DisplayName => "Русский";
    public override string EnglishName => "Russian";

    // Button
    public override string ButtonAddFile => "Добавить файл";
    public override string ButtonAddFolder => "Добавить папку";
    public override string ButtonAnalyze => "Анализ";
    public override string ButtonCalculateHash => "Вычислить хеш";
    public override string ButtonCancel => "Отмена";
    public override string ButtonCancelForceLanguage => "Отменить принудительный язык";
    public override string ButtonChange => "Изменить";
    public override string ButtonClear => "Очистить";
    public override string ButtonDownload => "Скачать";
    public override string ButtonFilterAll => "Все";
    public override string ButtonFilterGame => "Игра";
    public override string ButtonFilterUpdate => "Обновление";
    public override string ButtonForceLanguage => "Принудительный язык";
    public override string ButtonInstall => "Установить";
    public override string ButtonLater => "Позже";
    public override string ButtonLoadGameList => "Загрузить список игр";
    public override string ButtonOk => "ОК";
    public override string ButtonRecoverTitleDb => "Восстановить title.db";
    public override string ButtonRemoveSelected => "Удалить";
    public override string ButtonRepackFolderBased => "Перепаковка (на основе папки)";
    public override string ButtonReset => "Сбросить";
    public override string ButtonSelectFile => "Выбрать файл";
    public override string ButtonSettings => "Настройки";
    public override string ButtonStart => "Начать";
    public override string ButtonUnpack => "Распаковка";
    public override string ButtonUnpackAndRepackFull => "Распаковка + перепаковка (Full)";
    public override string ButtonUnpackAndRepackStreaming => "Распаковка + перепаковка (потоковая)";

    // Check
    public override string CheckExtraOption => "Дополнительная опция (cmd 0x0B)";
    public override string CheckVideoFreezeFix => "Исправление зависания видео (cmd 0x00)";

    // Description
    public override string DescArchivePasswordProtected => "Этот архив защищён паролем.";
    public override string DescAutoCompressByRule => "Формат выбирается автоматически по правилам ниже, затем выполняется сжатие или распаковка";
    public override string DescAutoMatchByExtension => "Сопоставление выполняется автоматически по расширению";
    public override string DescAutoVersionDateInName => "Считывает версию и дату из имени файла патча и показывает их в имени результирующего файла";
    public override string DescCalculateFileHash => "Вычисляет хеши файлов";
    public override string DescCertsBinUsedForCia => "Файл certs.bin используется при создании файлов CIA";
    public override string DescClickOrDropToChange => "Щёлкните или перетащите файл, чтобы изменить.";
    public override string DescConvertByRule => "Сжатие, распаковка и преобразование формата выполняются по правилам ниже";
    public override string DescConvertByRuleMutual => "Взаимное преобразование по правилам ниже";
    public override string DescConvertFormats => "Преобразование между перечисленными ниже форматами файлов";
    public override string DescCreateSingleTrackCue => "Создаёт CUE-файл с одним треком (несколько треков не поддерживаются)";
    public override string DescDecryptFiles => "Расшифровывает файлы";
    public override string DescExtractCertsBin => "Извлекает certs.bin в папку с исполняемым файлом";
    public override string DescInstallToConsole => "Устанавливает игры на консоль 3DS";
    public override string DescMp3WavAutoConvertAt3 => "Файлы MP3 и WAV при добавлении автоматически преобразуются в формат AT3.";
    public override string DescOutputFolderDefault => "При отключении сохраняет в папку output внутри папки исходного ROM";
    public override string DescPackMax5 => "Можно упаковать до 5 файлов";
    public override string DescPc98Hdi => "Находит и заменяет файлы внутри HDI (FAT12/16) по логическому пути. Путь монтирования определяется автоматически.";
    public override string DescRenameByFormat => "Пакетное переименование файлов по выбранному формату";
    public override string DescRestorePbpToBinCue => "Восстанавливает PBP в исходный BIN+CUE";
    public override string DescSelectSdLoadList => "Выберите SD-карту и загрузите список установленных игр";
    public override string DescTistoryEnterUrl => "Введите ссылку Tistory и нажмите «Анализ»";
    public override string DescTistoryFindAttachments => "Находит ссылки на вложения и добавляет их в список";
    public override string DescVitaAutoClassify => "App/Patch/AddCont классифицируются автоматически";
    public override string DescVitaGroupToNoNpDrm => "App/Patch/AddCont с одинаковым Title ID объединяются и преобразуются в формат NoNpDrm";
    public override string DescVitaKeyRequiredShared => "Требуется ключ (work.bin, *.rif/строка zRIF) (Patch может использовать ключ App)";
    public override string DescVitaPatchMerge => "Эмулятор: после расшифровки перезаписывает app/addcont / Консоль: добавляет rePatch/reAddcont";
    public override string DescVitaPkgKeyRequired => "Для pkg требуется ключ (work.bin, *.rif/строка zRIF)";
    public override string DescWiiUAutoClassify => "Игра/обновление/DLC классифицируются автоматически по Title ID";

    // Dialog
    public override string DialogSelectGameFile => "Выберите файл игры";

    // Error
    public override string ErrorNoMetadata => "Не найдено метаданных для анализа.";

    // Filter
    public override string FilterAllFiles => "Все файлы";
    public override string FilterSwitchFiles => "Файлы Switch";

    // Header
    public override string HeaderCompressionRatio => "Коэффициент сжатия";
    public override string HeaderConvert => "Преобразование";
    public override string HeaderFileName => "Имя файла";
    public override string HeaderFileNameCompare => "Сравнение имён файлов (текущее/новое)";
    public override string HeaderFilePath => "Путь";
    public override string HeaderForcedLanguage => "Фиксированный язык";
    public override string HeaderFormat => "Формат";
    public override string HeaderInput => "Ввод";
    public override string HeaderKey => "Ключ";
    public override string HeaderLocalTitle => "Локальное название";
    public override string HeaderOutput => "Вывод";
    public override string HeaderPatch => "Патч";
    public override string HeaderProductCode => "Код продукта";
    public override string HeaderResult => "Результат";
    public override string HeaderSize => "Размер";
    public override string HeaderTitle => "Название";
    public override string HeaderVersion => "Версия";

    // Hint
    public override string HintDragOrClickPatch3ds => "Перетащите или щёлкните, чтобы выбрать папку патча или файл zip/7z";
    public override string HintDragOrClickRiivolutionPatch => "Перетащите или щёлкните, чтобы выбрать папку патча Riivolution или zip/7z/xml";
    public override string HintDragOrClickRom3ds => "Перетащите или щёлкните, чтобы выбрать файл ROM (.3ds, .cci, .zcci, .cia)";
    public override string HintDragOrClickWiiDisc => "Перетащите или щёлкните, чтобы выбрать диск Wii (.iso, .wbfs, .wia, .rvz)";
    public override string HintDragOrSelectOutputFolder => "Перетащите или выберите папку вывода";
    public override string HintDragOrSelectWorkFolder => "Перетащите или выберите рабочую папку";
    public override string HintDropArcadeFiles => "Перетащите исходный ZIP и патчи (IPS, папка, ZIP) выше";
    public override string HintDropCueChd => "Перетащите файл CUE/CHD";
    public override string HintDropFile => "Перетащите файл сюда";
    public override string HintDropFileOrFolder => "Перетащите файлы или папки сюда";
    public override string HintDropGameFileOrFolder => "Перетащите файлы/папки игры сюда";
    public override string HintDropImageFile => "Перетащите файл изображения";
    public override string HintDropOrClickAudio => "Перетащите или щёлкните, чтобы выбрать аудиофайл";
    public override string HintDropPatchFile => "Перетащите файл патча";
    public override string HintDropSourceNsp => "Перетащите исходный файл NSP/NSZ";
    public override string HintDropVitaFiles => "Перетащите файлы (pkg, zip)/папки PS Vita сюда";
    public override string HintDropVitaPkg => "Перетащите файлы/папки PKG для PS Vita сюда";
    public override string HintDropWiiUFiles => "Перетащите папки WUP/Loadiine или файлы wud/wux/wua сюда";
    public override string HintRepackReplaceRule => "Игра и обновление заменяются автоматически, DLC добавляются";
    public override string HintSearchGame => "Поиск игр...";
    public override string HintSelectOutput => "Выберите папку вывода";
    public override string HintSoundCancel => "Отмена (перетащите файл .mp3 или .wav)";
    public override string HintSoundComplete => "Завершение (перетащите файл .mp3 или .wav)";
    public override string HintSoundError => "Ошибка (перетащите файл .mp3 или .wav)";
    public override string HintSplitExpandRule => "Разверните объединённый ROM кнопкой +, чтобы выбрать игру, обновление или DLC для разделения";

    // Label
    public override string LabelActual => "Фактически";
    public override string LabelAlgorithm => "Алгоритм:";
    public override string LabelAllFormats => "Все форматы";
    public override string LabelBackgroundMusic => "Фоновая музыка";
    public override string LabelBootLogo => "Загрузочный логотип";
    public override string LabelCompression => "Сжатие";
    public override string LabelCompressionAlgorithmIso => "Алгоритм сжатия (ISO: PSP, PS2)";
    public override string LabelCompressionLevel => "Уровень сжатия";
    public override string LabelCompressionLevelNszXcz => "Уровень сжатия (NSZ, XCZ)";
    public override string LabelCompressionMode => "Режим сжатия";
    public override string LabelDefaultImage => "Изображение по умолчанию";
    public override string LabelDownloadLocation => "Папка загрузки";
    public override string LabelEstimated => "Ожидаемо";
    public override string LabelExpectedFileName => "Ожидаемое имя файла:";
    public override string LabelFile => "Файл";
    public override string LabelFileCount(object arg0) => $"Файлов: {arg0}";
    public override string LabelFormat3dsCci => "3DS (тот же формат, что и CCI)";
    public override string LabelFormatBinCueSingleTrack => "BIN+CUE (один трек)";
    public override string LabelFormatChdIsoBased => "CHD (на основе ISO)";
    public override string LabelFormatList3dsCciCia => "3DS (тот же формат, что и CCI), CCI, CIA";
    public override string LabelFormatListIsoStandard => "ISO (стандартный), CUE+BIN, GDI+BIN+RAW, CCD+IMG+SUB";
    public override string LabelFormatListLoadiineFolderWua => "Папка Loadiine, WUA";
    public override string LabelFormatListWuaWupLoadiine => "WUA, папка WUP, папка Loadiine";
    public override string LabelFormatListWupFolderWua => "Папка WUP, WUA";
    public override string LabelFormatListWupLoadiine => "Папка WUP, папка Loadiine";
    public override string LabelGameId => "ID игры";
    public override string LabelGameName => "Название игры";
    public override string LabelGeneral => "Общие";
    public override string LabelLanguage => "Язык";
    public override string LabelLz4Compression => "Сжатие Lz4";
    public override string LabelMergeMode => "Режим объединения:";
    public override string LabelMultiRomIndex => "Индекс мульти-ROM";
    public override string LabelNoteCemuArchive => "(архив Cemu)";
    public override string LabelNoteDiscImage => "(образ диска)";
    public override string LabelNoteFolder => "(папка)";
    public override string LabelNspTitleId => "Title ID NSP";
    public override string LabelOptions => "Параметры:";
    public override string LabelOthers => "Прочие";
    public override string LabelOutputFormat => "Формат вывода:";
    public override string LabelOutputOptions => "Параметры вывода";
    public override string LabelPatch => "Патч";
    public override string LabelPatchInfo => "Информация о патче";
    public override string LabelPbpPacking => "Упаковка PBP";
    public override string LabelPkgCount(object arg0) => $"PKG: {arg0}";
    public override string LabelPopsEmulatorSettings => "Настройки эмулятора POPS";
    public override string LabelPublisher => "Издатель";
    public override string LabelQuality => "Качество";
    public override string LabelSelectPatch => "Выбор патча:";
    public override string LabelSettings => "Настройки";
    public override string LabelSoundVolume => "Громкость звука:";
    public override string LabelSource => "Исходный файл";
    public override string LabelSourceAutoExtractFormats => "Поддерживаемые форматы автоматической распаковки исходного файла:";
    public override string LabelSupportedLanguages => "Поддерживаемые языки";
    public override string LabelSupportedPatchFormats => "Поддерживаемые форматы патчей:";
    public override string LabelTistoryUrl => "URL Tistory";
    public override string LabelVersion => "Версия";
    public override string LabelWideScreen => "Широкий экран";

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
    public override string MenuClear => "Очистить";
    public override string MenuClearLog => "Очистить журнал";
    public override string MenuCopyLog => "Копировать журнал";
    public override string MenuDeselect => "Отменить выбор";
    public override string MenuExtractCci => "Извлечь как CCI";
    public override string MenuExtractCia => "Извлечь как CIA";
    public override string MenuFindInArchive => "Найти в архиве (zip/7z)...";
    public override string MenuFindInFolder => "Найти в папке...";
    public override string MenuOpenFolder => "Открыть папку";
    public override string MenuRemoveSelected => "Удалить";
    public override string MenuSelectArchive => "Выбрать архив (zip/7z)...";
    public override string MenuSelectFileIpsZip => "Выбрать файл (*.ips, *.zip)...";
    public override string MenuSelectFileZip7z => "Выбрать файл (*.zip, *.7z)...";
    public override string MenuSelectFileZip7zXml => "Выбрать файл (*.zip, *.7z, *.xml)...";
    public override string MenuSelectFolder => "Выбрать папку...";

    // Message
    public override string MsgTitleError => "Ошибка";
    public override string MsgTitleInfo => "Информация";
    public override string MsgTitleQuestion => "Подтверждение";
    public override string MsgTitleWarning => "Предупреждение";

    // Option
    public override string OptionLevel0None => "0: Без сжатия";
    public override string OptionLevel18Recommended => "18: Рекомендуется";
    public override string OptionLevel3Fastest => "3: Максимальная скорость";
    public override string OptionLevel5Balanced => "5: Баланс";
    public override string OptionLevel9Balanced => "9: Баланс";
    public override string OptionLevel9Max => "9: Максимум";

    // Radio
    public override string RadioBlock => "Блочный";
    public override string RadioBlockless => "Безблочный";
    public override string RadioCciEmulator => "CCI (эмулятор)";
    public override string RadioCiaConsole => "CIA (консоль)";
    public override string RadioConsoleRepatch => "Консоль (rePatch, reAddcont)";
    public override string RadioEmulatorAppAddcont => "Эмулятор (app, addcont)";
    public override string RadioFolderPerTitleId => "Папка (по Title ID)";
    public override string RadioLowercase => "Строчные";
    public override string RadioMergeAll => "Все";
    public override string RadioMergeGroupByTitleId => "Группировка по Title ID (прежний способ)";
    public override string RadioOutputGameId => "Консоль (Title ID/EBOOT.PBP)";
    public override string RadioOutputNormal(object arg0) => $"Обычный (название.{arg0})";
    public override string RadioQuality30s => "30 с (132k)";
    public override string RadioQuality60s => "60 с (66k)";
    public override string RadioRvzDolphin => "RVZ (сжатие только для Dolphin)";
    public override string RadioUppercase => "Заглавные";
    public override string RadioWbfsUsbLoader => "WBFS (USB-загрузчик)";
    public override string RadioWuaEmulator => "WUA (эмулятор)";
    public override string RadioWupConsole => "WUP (консоль)";
    public override string RadioZcciAzahar => "ZCCI (сжатие только для Azahar)";
    public override string RadioZipPerTitleId => "ZIP (по Title ID)";

    // Status
    public override string StatusAnalyzing => "Анализ...";
    public override string StatusConvertingToAt3 => "Преобразование в AT3...";
    public override string StatusDownloadingImage => "Загрузка изображения...";
    public override string StatusHashCancelledByPatch => "Вычисление хеша отменено из-за запуска патча";
    public override string StatusHashFailed => "Не удалось вычислить хеш";
    public override string StatusLoading => "Загрузка...";
    public override string StatusMatched => "Сопоставлено";
    public override string StatusNoKey => "Нет ключа";
    public override string StatusPresetLoaded => "Пресет загружен (ручные настройки отключены)";
    public override string StatusUnmatched => "Не сопоставлено";
    public override string StatusWrongPassword => "Неверный пароль.";

    // Tab
    public override string TabArcade => "Аркады";
    public override string TabCalculateHash => "Вычисление хеша";
    public override string TabConvert => "Преобразование";
    public override string TabCreateCue => "Создание Cue";
    public override string TabDecrypt => "Расшифровка";
    public override string TabExtractCertsBin => "Извлечение certs.bin";
    public override string TabFormatConvert => "Преобразование формата";
    public override string TabGeneral => "Общие";
    public override string TabIgnoreVersion => "Игнорировать версию";
    public override string TabInstall => "Установка";
    public override string TabInstalledGames => "Установленные игры";
    public override string TabInstallNewGame => "Установить новую игру";
    public override string TabKeyFile => "Файл ключей";
    public override string TabLz4Compression => "Сжатие Lz4";
    public override string TabMerge => "Объединение";
    public override string TabPatch => "Патч";
    public override string TabPbpPacking => "Упаковка PBP";
    public override string TabPbpUnpack => "Распаковка PBP";
    public override string TabRename => "Переименование файлов";
    public override string TabRepack => "Перепаковка";
    public override string TabSaturnNspConvert => "Преобразование Saturn NSP";
    public override string TabSettings => "Настройки";
    public override string TabSplit => "Разделение";
    public override string TabTistoryDownloader => "Загрузчик Tistory";
    public override string TabUtil => "Утилиты";

    // Title
    public override string TitleArchivePassword => "Пароль архива";

    // Toggle
    public override string ToggleAlwaysOnTop => "Всегда поверх других окон";
    public override string ToggleAutoCompressAfterPatch => "Автоматически сжимать после патча";
    public override string ToggleAutoExtractDelete => "Автоматическая распаковка архивов (удалить оригинал после завершения)";
    public override string ToggleAutoVersionDateInName => "Автоматически добавлять версию/дату в имя результирующего файла";
    public override string ToggleChangeOutputFolder => "Изменить папку вывода";
    public override string ToggleCheckUpdateOnStart => "Проверять обновления при запуске";
    public override string ToggleMergePatchToGame => "Объединить патч с игрой";
    public override string ToggleOtherFormats => "Прочие";
    public override string ToggleSoundEffects => "Звуковые эффекты";

    // ToolTip
    public override string ToolTipAddFile => "Добавить файл";
    public override string ToolTipAddFolder => "Добавить папку";
    public override string ToolTipAddSequentialPatch => "Добавить патч для последовательного применения";
    public override string ToolTipAutoExtractDelete => "Если скачанный файл является архивом (zip/7z/rar), он автоматически распаковывается, а исходный архив удаляется";
    public override string ToolTipBatchKoreanPatch => "Массовое назначение корейских патчей (корневая папка или архив)";
    public override string ToolTipBlocklessMode => "Последовательный доступ (приоритет объёма)";
    public override string ToolTipBlockMode => "Случайный доступ (приоритет скорости)";
    public override string ToolTipBrowseOutput => "Выбрать папку вывода";
    public override string ToolTipChangePatchFile => "Выбрать другой файл патча";
    public override string ToolTipChangePatchPackage => "Выбрать другой пакет патчей";
    public override string ToolTipClear => "Очистить";
    public override string ToolTipCopyHash => "Копировать хеш";
    public override string ToolTipDragFolderToChangeDownload => "Перетащите папку сюда, чтобы изменить папку загрузки.";
    public override string ToolTipExpandCollapse => "Развернуть/Свернуть";
    public override string ToolTipForcedLanguage => "Фиксирует региональные настройки на этом языке при установке (locale.txt создаётся автоматически). Отображаются только языки, реально присутствующие в SMDH.";
    public override string ToolTipForceLanguage => "При перепаковке включаются только выбранные языки, остальные отключаются.";
    public override string ToolTipGameNameMax63 => "Название игры (макс. 63 символа)";
    public override string ToolTipLicenseKey => "Путь к файлу лицензии (work.bin, *.rif)/строка zRIF";
    public override string ToolTipLicenseKeyPatchFallback => "Путь к файлу лицензии (work.bin, *.rif)/строка zRIF (для Patch при пустом поле используется ключ App)";
    public override string ToolTipMergeAll => "Объединяет все элементы в один файл.";
    public override string ToolTipMergeGroupByTitleId => "Группирует элементы по Title ID и объединяет каждую группу в отдельный файл.";
    public override string ToolTipMovableSedGuide => "1. Запустите GodMode9 (START + POWER)\n2. Выберите SYSNAND CTRNAND → private → movable.sed\n3. Выполните Copy to 0:/gm9/out в самом низу";
    public override string ToolTipOpenGithubReleases => "Открывает страницу релизов на GitHub";
    public override string ToolTipOpenHelp => "Открывает страницу справки";
    public override string ToolTipOpenWorkSpace => "Открыть папку вывода";
    public override string ToolTipPlayPause => "Воспроизведение/Пауза";
    public override string ToolTipPublisherMax63 => "Издатель (макс. 63 символа)";
    public override string ToolTipRecoverTitleDb => "Используйте, если установленная на консоль 3DS игра не появляется в меню HOME или не распознаётся из-за ошибки title.db.\n\nПересоздаёт title.db и import.db на основе игр, установленных на SD-карте.\nСуществующие title.db/import.db удаляются; файлы игр не изменяются.\nЕсли всё распознаётся нормально, нажимать не нужно.";
    public override string ToolTipRemoveSelected => "Удалить";
    public override string ToolTipRestoreDefaultPath => "Восстановить расположение по умолчанию";
    public override string ToolTipRomFsLz4 => "Сжатие RomFS в Lz4 (применяется при перепаковке). Игра и обновление объединяются в одно, поэтому сжатие включается, если отмечено любое из них.";
    public override string ToolTipSelectAllToggle => "Выбрать/снять выбор со всех";
    public override string ToolTipSelectFile => "Выбрать файл";
    public override string ToolTipTistoryUrlInput => "Вводите по одной ссылке на запись Tistory в строке (можно несколько, Ctrl+Enter запускает анализ)";
    public override string ToolTipTitleIdInput => "16-значное шестнадцатеричное число (например: 0100000000010000). Оставьте пустым, чтобы сохранить исходный Title ID.";
    public override string ToolTipValidateCompress => "Выполняет дополнительную проверку после сжатия";

    // Update
    public override string LatestVersion => "Последняя версия";
    public override string NoUpdateNotes => "Нет информации об обновлениях.";
    public override string UpdateAvailable => "Доступно обновление";
    public override string UpdateDescription => "Выпущена новая версия RomForge.";
    public override string UpdateTitle => "Обновление RomForge";

    public override string StatusWaiting => "Ожидание";
    public override string StatusConverting => "Конвертация";
    public override string StatusDecrypting => "Расшифровка";
    public override string StatusExtracting => "Извлечение";
    public override string StatusDownloading => "Загрузка";
    public override string StatusUnzipping => "Распаковка";
    public override string StatusUnzipDone => "Распаковано";
    public override string StatusUnzipFailed => "Ошибка распаковки";
    public override string StatusDone => "Готово";
    public override string StatusFailed => "Ошибка";
    public override string StatusCancelled => "Отменено";
    public override string StatusUnsupported => "Не поддерживается";
    public override string StatusSkipped => "Пропущено";
    public override string RenameStatusReady => "Будет переименован";
    public override string RenameStatusUnchanged => "Без изменений";
    public override string RenameStatusMissingInfo => "Нет данных";
    public override string RenameStatusEmptyName => "Нет имени";
    public override string RenameStatusNameTooLong => "Слишком длинное имя";
    public override string RenameStatusDuplicate => "Имя дублируется";
    public override string RenameStatusTargetExists => "Файл существует";

    public override string ProgressElapsed(object arg0) => $"Прошло {arg0}";
    public override string ProgressCompletedCount(object arg0, object arg1) => $"Готово: {arg0} / {arg1}";
    public override string ProgressHashingPercent(object arg0) => $"Вычисление хеша... {arg0}%";
    public override string ProgressAnalyzingPatchInfo => "Анализ информации о патче...";
    public override string ProgressExtractingPatch => "Распаковка патча...";
    public override string ProgressUnpackingFile(object arg0) => $"Распаковка: {arg0}";
    public override string ProgressRepackingFile(object arg0) => $"Перепаковка: {arg0}";
    public override string ProgressApplyingPatchTo(object arg0) => $"Применение патча: {arg0}";
    public override string ProgressHashingPartitions(object arg0) => $"Вычисление хешей разделов: {arg0}";
    public override string ProgressCompressingWbfs(object arg0) => $"Сжатие WBFS: {arg0}";
    public override string ProgressCompressingRvz(object arg0) => $"Сжатие RVZ: {arg0}";
    public override string ProgressCreatingIso(object arg0) => $"Создание ISO: {arg0}";
    public override string ProgressWupCreated(object arg0, object arg1) => $"WUP создан: {arg0}_v{arg1}";
    public override string MsgCancelled => "Отменено.";
    public override string StatusInitializing => "Инициализация...";
    public override string StatusSelectSdAndMovable => "Выберите SD-карту и movable.sed";
    public override string ProgressLoadingCount(object arg0, object arg1) => $"Загрузка... {arg0} / {arg1}";
    public override string ProgressScanningCount(object arg0, object arg1) => $"Сканирование... {arg0} / {arg1}";
    public override string StatusRecoveringTitleDb => "Восстановление title.db...";
    public override string StatusRecoveryDone(object arg0) => $"Восстановление завершено: {arg0} назв.";
    public override string StatusExtractingTitle(object? arg0, object arg1) => $"Извлечение: {arg0} - {arg1}";
    public override string StatusInstallStarted(object? arg0) => $"Установка начата: {arg0}";
    public override string StatusInstallDone(object? arg0) => $"Установка завершена: {arg0}";
    public override string StatusScanningTitles => "Сканирование названий...";
    public override string StatusLoadingMetadata(object arg0) => $"Загрузка метаданных... (всего {arg0})";
    public override string StatusTitlesLoaded(object arg0) => $"Загружено названий: {arg0}";
    public override string ProgressPreparing => "Подготовка";
    public override string ProgressHashing => "Вычисление хеша";
    public override string ProgressCompressingNsz => "Сжатие NSZ";
}