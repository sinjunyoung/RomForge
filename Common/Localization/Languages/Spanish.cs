namespace Common.Localization.Languages;

public sealed class Spanish : LocalizedStrings
{
    // Language
    public override string Code => "es-ES";
    public override string DisplayName => "Español";
    public override string EnglishName => "Spanish";

    // Button
    public override string ButtonAddFile => "Añadir archivo";
    public override string ButtonAddFolder => "Añadir carpeta";
    public override string ButtonAnalyze => "Analizar";
    public override string ButtonCalculateHash => "Calcular hash";
    public override string ButtonCancel => "Cancelar";
    public override string ButtonCancelForceLanguage => "Cancelar idioma forzado";
    public override string ButtonChange => "Cambiar";
    public override string ButtonClear => "Vaciar";
    public override string ButtonDownload => "Descargar";
    public override string ButtonFilterAll => "Todo";
    public override string ButtonFilterGame => "Juego base";
    public override string ButtonFilterUpdate => "Actualización";
    public override string ButtonForceLanguage => "Forzar idioma";
    public override string ButtonInstall => "Instalar";
    public override string ButtonLater => "Más tarde";
    public override string ButtonLoadGameList => "Cargar lista de juegos";
    public override string ButtonOk => "Aceptar";
    public override string ButtonRecoverTitleDb => "Recuperar title.db";
    public override string ButtonRemoveSelected => "Eliminar";
    public override string ButtonRepackFolderBased => "Reempaquetar (basado en carpeta)";
    public override string ButtonReset => "Restablecer";
    public override string ButtonSelectFile => "Seleccionar archivo";
    public override string ButtonSettings => "Configuración";
    public override string ButtonStart => "Iniciar";
    public override string ButtonUnpack => "Desempaquetar";
    public override string ButtonUnpackAndRepackFull => "Desempaquetar + Reempaquetar (Full)";
    public override string ButtonUnpackAndRepackStreaming => "Desempaquetar + Reempaquetar (basado en streaming)";

    // Check
    public override string CheckExtraOption => "Opción adicional (cmd 0x0B)";
    public override string CheckVideoFreezeFix => "Corrección de congelación de vídeo (cmd 0x00)";

    // Description
    public override string DescArchivePasswordProtected => "Este archivo comprimido está protegido con contraseña.";
    public override string DescAutoCompressByRule => "El formato se selecciona automáticamente según las reglas siguientes y se comprime o descomprime";
    public override string DescAutoMatchByExtension => "La coincidencia se realiza automáticamente según la extensión";
    public override string DescAutoVersionDateInName => "Lee la versión y la fecha del nombre del archivo de parche y las muestra en el nombre del archivo resultante";
    public override string DescCalculateFileHash => "Calcula los hashes de los archivos";
    public override string DescCertsBinUsedForCia => "El archivo certs.bin se usa al crear archivos CIA";
    public override string DescClickOrDropToChange => "Haz clic o suelta un archivo para cambiarlo.";
    public override string DescConvertByRule => "La compresión, descompresión y conversión de formato se realizan según las reglas siguientes";
    public override string DescConvertByRuleMutual => "Convierte entre formatos según las reglas siguientes";
    public override string DescConvertFormats => "Convierte entre los siguientes formatos de archivo";
    public override string DescCreateSingleTrackCue => "Crea un archivo CUE de una sola pista (sin compatibilidad con varias pistas)";
    public override string DescDecryptFiles => "Descifra archivos";
    public override string DescExtractCertsBin => "Extrae certs.bin en la ubicación del ejecutable";
    public override string DescInstallToConsole => "Instala juegos en una consola 3DS";
    public override string DescMp3WavAutoConvertAt3 => "Los archivos MP3 y WAV se convierten automáticamente al formato AT3 al añadirlos.";
    public override string DescOutputFolderDefault => "Si se desactiva, se guarda en la carpeta output dentro de la carpeta de la ROM de origen";
    public override string DescPackMax5 => "Se pueden empaquetar hasta 5 archivos";
    public override string DescPc98Hdi => "Busca y reemplaza archivos internos del HDI (FAT12/16) por ruta lógica. La ruta de montaje se detecta automáticamente.";
    public override string DescRenameByFormat => "Cambia el nombre de los archivos por lotes según el formato seleccionado";
    public override string DescRestorePbpToBinCue => "Restaura el PBP al BIN+CUE original";
    public override string DescSelectSdLoadList => "Selecciona la tarjeta SD y carga la lista de instalación";
    public override string DescTistoryEnterUrl => "Introduce una URL de Tistory y haz clic en Analizar";
    public override string DescTistoryFindAttachments => "Busca enlaces de archivos adjuntos y los añade a la lista";
    public override string DescVitaAutoClassify => "App/Patch/AddCont se clasifican automáticamente";
    public override string DescVitaGroupToNoNpDrm => "App/Patch/AddCont con el mismo Title ID se agrupan y se convierten al formato NoNpDrm";
    public override string DescVitaKeyRequiredShared => "Se requiere una clave (work.bin, *.rif/cadena zRIF) (Patch puede compartir la clave de App)";
    public override string DescVitaPatchMerge => "Emulador: sobrescribe app/addcont tras descifrar / Consola: añade rePatch/reAddcont";
    public override string DescVitaPkgKeyRequired => "Para pkg se requiere una clave (work.bin, *.rif/cadena zRIF)";
    public override string DescWiiUAutoClassify => "El juego base, la actualización y los DLC se clasifican automáticamente por Title ID";

    // Dialog
    public override string DialogSelectGameFile => "Seleccionar archivo del juego";

    // Error
    public override string ErrorNoMetadata => "No hay metadatos analizables disponibles.";

    // Filter
    public override string FilterAllFiles => "Todos los archivos";
    public override string FilterSwitchFiles => "Archivos de Switch";

    // Header
    public override string HeaderCompressionRatio => "Relación de compresión";
    public override string HeaderConvert => "Conversión";
    public override string HeaderFileName => "Nombre del archivo";
    public override string HeaderFileNameCompare => "Comparación de nombres (actual/nuevo)";
    public override string HeaderFilePath => "Ruta";
    public override string HeaderForcedLanguage => "Idioma fijo";
    public override string HeaderFormat => "Formato";
    public override string HeaderInput => "Entrada";
    public override string HeaderKey => "Clave";
    public override string HeaderLocalTitle => "Título local";
    public override string HeaderOutput => "Salida";
    public override string HeaderPatch => "Parche";
    public override string HeaderProductCode => "Código de producto";
    public override string HeaderResult => "Resultado";
    public override string HeaderSize => "Tamaño";
    public override string HeaderTitle => "Título";
    public override string HeaderVersion => "Versión";

    // Hint
    public override string HintDragOrClickPatch3ds => "Arrastra o haz clic para elegir una carpeta de parche o un archivo zip/7z";
    public override string HintDragOrClickRiivolutionPatch => "Arrastra o haz clic para elegir una carpeta de parche Riivolution o un zip/7z/xml";
    public override string HintDragOrClickRom3ds => "Arrastra o haz clic para elegir un archivo ROM (.3ds, .cci, .zcci, .cia)";
    public override string HintDragOrClickWiiDisc => "Arrastra o haz clic para elegir un disco de Wii (.iso, .wbfs, .wia, .rvz)";
    public override string HintDragOrSelectOutputFolder => "Arrastra o selecciona la carpeta de salida";
    public override string HintDragOrSelectWorkFolder => "Arrastra o selecciona la carpeta de trabajo";
    public override string HintDropArcadeFiles => "Arrastra y suelta el ZIP de origen y los parches (IPS, carpeta, ZIP) arriba";
    public override string HintDropCueChd => "Arrastra y suelta un archivo CUE/CHD";
    public override string HintDropFile => "Arrastra y suelta un archivo aquí";
    public override string HintDropFileOrFolder => "Arrastra y suelta archivos o carpetas aquí";
    public override string HintDropGameFileOrFolder => "Arrastra y suelta archivos/carpetas de juego aquí";
    public override string HintDropImageFile => "Arrastra y suelta un archivo de imagen";
    public override string HintDropOrClickAudio => "Arrastra y suelta o haz clic para elegir un archivo de audio";
    public override string HintDropPatchFile => "Arrastra y suelta un archivo de parche";
    public override string HintDropSourceNsp => "Arrastra y suelta el archivo NSP/NSZ de origen";
    public override string HintDropVitaFiles => "Arrastra y suelta archivos (pkg, zip)/carpetas de PS Vita aquí";
    public override string HintDropVitaPkg => "Arrastra y suelta archivos/carpetas PKG de PS Vita aquí";
    public override string HintDropWiiUFiles => "Arrastra y suelta carpetas WUP/Loadiine o archivos wud/wux/wua aquí";
    public override string HintRepackReplaceRule => "El juego base y la actualización se reemplazan automáticamente; los DLC se añaden";
    public override string HintSearchGame => "Buscar juegos...";
    public override string HintSelectOutput => "Selecciona la carpeta de salida";
    public override string HintSoundCancel => "Cancelado (arrastra y suelta un archivo .mp3 o .wav)";
    public override string HintSoundComplete => "Completado (arrastra y suelta un archivo .mp3 o .wav)";
    public override string HintSoundError => "Error (arrastra y suelta un archivo .mp3 o .wav)";
    public override string HintSplitExpandRule => "Despliega una ROM combinada con el botón + para elegir el juego base, la actualización o los DLC que quieres dividir";

    // Label
    public override string LabelActual => "Real";
    public override string LabelAlgorithm => "Algoritmo:";
    public override string LabelAllFormats => "Todos los formatos";
    public override string LabelBackgroundMusic => "Música de fondo";
    public override string LabelBootLogo => "Logo de arranque";
    public override string LabelCompression => "Compresión";
    public override string LabelCompressionAlgorithmIso => "Algoritmo de compresión (ISO: PSP, PS2)";
    public override string LabelCompressionLevel => "Nivel de compresión";
    public override string LabelCompressionLevelNszXcz => "Nivel de compresión (NSZ, XCZ)";
    public override string LabelCompressionMode => "Modo de compresión";
    public override string LabelDefaultImage => "Imagen predeterminada";
    public override string LabelDownloadLocation => "Ubicación de descarga";
    public override string LabelEstimated => "Estimado";
    public override string LabelExpectedFileName => "Nombre de archivo previsto:";
    public override string LabelFile => "Archivo";
    public override string LabelFileCount(object arg0) => $"{arg0} archivos";
    public override string LabelFormat3dsCci => "3DS (mismo formato que CCI)";
    public override string LabelFormatBinCueSingleTrack => "BIN+CUE (pista única)";
    public override string LabelFormatChdIsoBased => "CHD (basado en ISO)";
    public override string LabelFormatList3dsCciCia => "3DS (mismo formato que CCI), CCI, CIA";
    public override string LabelFormatListIsoStandard => "ISO (estándar), CUE+BIN, GDI+BIN+RAW, CCD+IMG+SUB";
    public override string LabelFormatListLoadiineFolderWua => "Carpeta Loadiine, WUA";
    public override string LabelFormatListWuaWupLoadiine => "WUA, carpeta WUP, carpeta Loadiine";
    public override string LabelFormatListWupFolderWua => "Carpeta WUP, WUA";
    public override string LabelFormatListWupLoadiine => "Carpeta WUP, carpeta Loadiine";
    public override string LabelGameId => "ID del juego";
    public override string LabelGameName => "Nombre del juego";
    public override string LabelGeneral => "General";
    public override string LabelLanguage => "Idioma";
    public override string LabelLz4Compression => "Compresión Lz4";
    public override string LabelMergeMode => "Modo de fusión:";
    public override string LabelMultiRomIndex => "Índice multi-ROM";
    public override string LabelNoteCemuArchive => "(archivo de Cemu)";
    public override string LabelNoteDiscImage => "(imagen de disco)";
    public override string LabelNoteFolder => "(carpeta)";
    public override string LabelNspTitleId => "Title ID del NSP";
    public override string LabelOptions => "Opciones:";
    public override string LabelOthers => "Otros";
    public override string LabelOutputFormat => "Formato de salida:";
    public override string LabelOutputOptions => "Opciones de salida";
    public override string LabelPatch => "Parche";
    public override string LabelPatchInfo => "Información del parche";
    public override string LabelPbpPacking => "Empaquetado PBP";
    public override string LabelPkgCount(object arg0) => $"{arg0} archivos PKG";
    public override string LabelPopsEmulatorSettings => "Configuración del emulador POPS";
    public override string LabelPublisher => "Editor";
    public override string LabelQuality => "Calidad";
    public override string LabelSelectPatch => "Seleccionar parche:";
    public override string LabelSettings => "Configuración";
    public override string LabelSoundVolume => "Volumen de sonido:";
    public override string LabelSource => "Origen";
    public override string LabelSourceAutoExtractFormats => "Formatos compatibles con la extracción automática del origen:";
    public override string LabelSupportedLanguages => "Idiomas compatibles";
    public override string LabelSupportedPatchFormats => "Formatos de parche compatibles:";
    public override string LabelTistoryUrl => "URL de Tistory";
    public override string LabelVersion => "Versión";
    public override string LabelWideScreen => "Pantalla panorámica";

    // Log
    public override string LogAnalyzeMetadata => "━━ Analizando metadatos... ━━";
    public override string LogCompressAndMerge => "Comprimiendo y fusionando...";
    public override string LogCompressAndSplit => "Comprimiendo y dividiendo...";
    public override string LogCompressionRatio => "Relación de compresión";
    public override string LogDecompressAndMerge => "Descomprimiendo y fusionando";
    public override string LogDeleteIncompleteFile => "Se eliminó el archivo creado de forma incompleta.";
    public override string LogError(object arg0) => $"Ocurrió un error: {arg0}";
    public override string LogFinalId(object arg0, object arg1) => $"ID final: {arg0} / Versión: {arg1}";
    public override string LogMergeComplete(object arg0) => $"Fusión completada: {arg0}";
    public override string LogMergeFailed(object? arg0, object arg1) => $"[{arg0}] Error al fusionar: {arg1}";
    public override string LogMerging => "Fusionando...";
    public override string LogModeCompress => "Comprimir";
    public override string LogModeDecompress => "Descomprimir";
    public override string LogProcessError => "Error de procesamiento";
    public override string LogProcessStart => "Iniciar";
    public override string LogSplitComplete(object arg0) => $"División completada: {arg0}";
    public override string LogSplitFailed(object arg0, object arg1) => $"Error al dividir (ID: {arg0}): {arg1}";
    public override string LogSplitPreparing(object arg0) => $"{arg0} Iniciando división";
    public override string LogSplitting => "Separando...";
    public override string LogStatusCompressing => "Comprimiendo...";
    public override string LogStatusCopying => "Copiando...";
    public override string LogStatusDecompressing => "Descomprimiendo...";
    public override string LogStatusDone => "Hecho";
    public override string LogStatusValidating => "Validando...";
    public override string LogTitleGroupDetected(object arg0) => $"Se detectaron {arg0} grupos de títulos";
    public override string LogValidationComplete => "Validación completada";
    public override string LogValidationStart => "Iniciando validación";

    // Main
    public override string MainErrNoFiles => "Agrega archivos y vuelve a intentarlo.";
    public override string MainErrNoKeys => "No se encontró el archivo prod.keys. Coloca el archivo de claves en la carpeta de la aplicación.";
    public override string MainErrNoOutput => "Configura la carpeta de salida.";
    public override string MainLogAllComplete(object arg0) => $"✓ Todas las tareas completadas. Tiempo total: {arg0}";
    public override string MainLogRecalculating => "Reanalizando tipos de archivo...";
    public override string MainMsgDone => "¡Completado!";
    public override string MainMsgSplitDone => "División completada.";

    // Menu
    public override string MenuClear => "Vaciar";
    public override string MenuClearLog => "Borrar registro";
    public override string MenuCopyLog => "Copiar registro";
    public override string MenuDeselect => "Anular selección";
    public override string MenuExtractCci => "Extraer como CCI";
    public override string MenuExtractCia => "Extraer como CIA";
    public override string MenuFindInArchive => "Buscar en archivo comprimido (zip/7z)...";
    public override string MenuFindInFolder => "Buscar en carpeta...";
    public override string MenuOpenFolder => "Abrir carpeta";
    public override string MenuRemoveSelected => "Eliminar";
    public override string MenuSelectArchive => "Seleccionar archivo comprimido (zip/7z)...";
    public override string MenuSelectFileIpsZip => "Seleccionar archivo (*.ips, *.zip)...";
    public override string MenuSelectFileZip7z => "Seleccionar archivo (*.zip, *.7z)...";
    public override string MenuSelectFileZip7zXml => "Seleccionar archivo (*.zip, *.7z, *.xml)...";
    public override string MenuSelectFolder => "Seleccionar carpeta...";

    // Message
    public override string MsgTitleError => "Error";
    public override string MsgTitleInfo => "Información";
    public override string MsgTitleQuestion => "Confirmación";
    public override string MsgTitleWarning => "Advertencia";

    // Option
    public override string OptionLevel0None => "0: Sin compresión";
    public override string OptionLevel18Recommended => "18: Recomendado";
    public override string OptionLevel3Fastest => "3: Velocidad máxima";
    public override string OptionLevel5Balanced => "5: Equilibrado";
    public override string OptionLevel9Balanced => "9: Equilibrado";
    public override string OptionLevel9Max => "9: Máximo";

    // Radio
    public override string RadioBlock => "Bloque";
    public override string RadioBlockless => "Sin bloques";
    public override string RadioCciEmulator => "CCI (emulador)";
    public override string RadioCiaConsole => "CIA (consola)";
    public override string RadioConsoleRepatch => "Consola (rePatch, reAddcont)";
    public override string RadioEmulatorAppAddcont => "Emulador (app, addcont)";
    public override string RadioFolderPerTitleId => "Carpeta (por Title ID)";
    public override string RadioLowercase => "Minúsculas";
    public override string RadioMergeAll => "Todo";
    public override string RadioMergeGroupByTitleId => "Agrupar por Title ID (método anterior)";
    public override string RadioOutputGameId => "Consola (Title ID/EBOOT.PBP)";
    public override string RadioOutputNormal(object arg0) => $"Normal (nombre del título.{arg0})";
    public override string RadioQuality30s => "30 s (132k)";
    public override string RadioQuality60s => "60 s (66k)";
    public override string RadioRvzDolphin => "RVZ (compresión exclusiva de Dolphin)";
    public override string RadioUppercase => "Mayúsculas";
    public override string RadioWbfsUsbLoader => "WBFS (cargador USB)";
    public override string RadioWuaEmulator => "WUA (emulador)";
    public override string RadioWupConsole => "WUP (consola)";
    public override string RadioZcciAzahar => "ZCCI (compresión exclusiva de Azahar)";
    public override string RadioZipPerTitleId => "ZIP (por Title ID)";

    // Status
    public override string StatusAnalyzing => "Analizando...";
    public override string StatusConvertingToAt3 => "Convirtiendo a AT3...";
    public override string StatusDownloadingImage => "Descargando imagen...";
    public override string StatusHashCancelledByPatch => "El cálculo del hash se canceló porque se inició el parche";
    public override string StatusHashFailed => "Error al calcular el hash";
    public override string StatusLoading => "Cargando...";
    public override string StatusMatched => "Emparejado";
    public override string StatusNoKey => "Sin clave";
    public override string StatusPresetLoaded => "Preajuste cargado (configuración manual desactivada)";
    public override string StatusUnmatched => "Sin emparejar";
    public override string StatusWrongPassword => "La contraseña es incorrecta.";

    // Tab
    public override string TabArcade => "Arcade";
    public override string TabCalculateHash => "Cálculo de hash";
    public override string TabConvert => "Conversión";
    public override string TabCreateCue => "Crear Cue";
    public override string TabDecrypt => "Descifrado";
    public override string TabExtractCertsBin => "Extraer certs.bin";
    public override string TabFormatConvert => "Conversión de formato";
    public override string TabGeneral => "General";
    public override string TabIgnoreVersion => "Ignorar versión";
    public override string TabInstall => "Instalación";
    public override string TabInstalledGames => "Juegos instalados";
    public override string TabInstallNewGame => "Instalar juego nuevo";
    public override string TabKeyFile => "Archivo de claves";
    public override string TabLz4Compression => "Compresión Lz4";
    public override string TabMerge => "Fusionar";
    public override string TabPatch => "Parche";
    public override string TabPbpPacking => "Empaquetado PBP";
    public override string TabPbpUnpack => "Desempaquetado PBP";
    public override string TabRename => "Renombrar archivos";
    public override string TabRepack => "Reempaquetar";
    public override string TabSaturnNspConvert => "Conversión NSP de Saturn";
    public override string TabSettings => "Configuración";
    public override string TabSplit => "Dividir";
    public override string TabTistoryDownloader => "Descargador de Tistory";
    public override string TabUtil => "Utilidades";

    // Title
    public override string TitleArchivePassword => "Contraseña del archivo comprimido";

    // Toggle
    public override string ToggleAlwaysOnTop => "Mostrar siempre sobre otras ventanas";
    public override string ToggleAutoCompressAfterPatch => "Comprimir automáticamente tras el parche";
    public override string ToggleAutoExtractDelete => "Extraer archivos comprimidos automáticamente (eliminar el original al terminar)";
    public override string ToggleAutoVersionDateInName => "Añadir automáticamente versión/fecha al nombre del archivo resultante";
    public override string ToggleChangeOutputFolder => "Cambiar carpeta de salida";
    public override string ToggleCheckUpdateOnStart => "Buscar actualizaciones al iniciar";
    public override string ToggleMergePatchToGame => "Fusionar parche con el juego";
    public override string ToggleOtherFormats => "Otros";
    public override string ToggleSoundEffects => "Efectos de sonido";

    // ToolTip
    public override string ToolTipAddFile => "Añadir archivo";
    public override string ToolTipAddFolder => "Añadir carpeta";
    public override string ToolTipAddSequentialPatch => "Añadir un parche para aplicar en secuencia";
    public override string ToolTipAutoExtractDelete => "Si un archivo descargado es un archivo comprimido (zip/7z/rar), se extrae automáticamente y se elimina el original";
    public override string ToolTipBatchKoreanPatch => "Asignar parches de coreano por lotes (carpeta raíz o archivo comprimido)";
    public override string ToolTipBlocklessMode => "Acceso secuencial (prioridad de espacio)";
    public override string ToolTipBlockMode => "Acceso aleatorio (prioridad de velocidad)";
    public override string ToolTipBrowseOutput => "Seleccionar carpeta de salida";
    public override string ToolTipChangePatchFile => "Cambiar a otro archivo de parche";
    public override string ToolTipChangePatchPackage => "Cambiar a otro paquete de parches";
    public override string ToolTipClear => "Vaciar";
    public override string ToolTipCopyHash => "Copiar valor hash";
    public override string ToolTipDragFolderToChangeDownload => "Puedes arrastrar una carpeta aquí para cambiar la ubicación de descarga.";
    public override string ToolTipExpandCollapse => "Expandir/Contraer";
    public override string ToolTipForcedLanguage => "Fija la configuración regional a este idioma al instalar (locale.txt se genera automáticamente). Solo se muestran los idiomas que existen realmente en el SMDH.";
    public override string ToolTipForceLanguage => "Al reempaquetar, solo se activan los idiomas seleccionados y se desactivan todos los demás.";
    public override string ToolTipGameNameMax63 => "Nombre del juego (máx. 63 caracteres)";
    public override string ToolTipLicenseKey => "Ruta del archivo de licencia (work.bin, *.rif)/cadena zRIF";
    public override string ToolTipLicenseKeyPatchFallback => "Ruta del archivo de licencia (work.bin, *.rif)/cadena zRIF (Patch usa la clave de App si se deja vacío)";
    public override string ToolTipMergeAll => "Fusiona todos los elementos en un solo archivo.";
    public override string ToolTipMergeGroupByTitleId => "Agrupa los elementos por Title ID y fusiona cada grupo en un archivo independiente.";
    public override string ToolTipMovableSedGuide => "1. Ejecuta GodMode9 (START + POWER)\n2. Selecciona SYSNAND CTRNAND → private → movable.sed\n3. Ejecuta Copy to 0:/gm9/out al final de la lista";
    public override string ToolTipOpenGithubReleases => "Abre la página de versiones de GitHub";
    public override string ToolTipOpenHelp => "Abre la página de ayuda";
    public override string ToolTipOpenWorkSpace => "Abrir carpeta de salida";
    public override string ToolTipPlayPause => "Reproducir/Pausa";
    public override string ToolTipPublisherMax63 => "Editor (máx. 63 caracteres)";
    public override string ToolTipRecoverTitleDb => "Úsalo cuando un juego instalado en una consola 3DS no aparece en el menú HOME o no se reconoce por un error de title.db.\n\nVuelve a crear title.db e import.db a partir de los títulos instalados en la tarjeta SD.\nSe eliminan los title.db/import.db existentes; los archivos de juego no se modifican.\nSi todo se reconoce con normalidad, no es necesario pulsarlo.";
    public override string ToolTipRemoveSelected => "Eliminar";
    public override string ToolTipRestoreDefaultPath => "Restaurar ubicación predeterminada";
    public override string ToolTipRomFsLz4 => "Compresión Lz4 de RomFS (se aplica al reempaquetar). El juego base y la actualización se combinan en uno, por lo que marcar cualquiera de los dos activa la compresión.";
    public override string ToolTipSelectAllToggle => "Seleccionar/deseleccionar todo";
    public override string ToolTipSelectFile => "Seleccionar archivo";
    public override string ToolTipTistoryUrlInput => "Introduce una URL de publicación de Tistory por línea (se permiten varias, Ctrl+Intro para analizar)";
    public override string ToolTipTitleIdInput => "Número hexadecimal de 16 dígitos (p. ej.: 0100000000010000). Déjalo vacío para conservar el Title ID original.";
    public override string ToolTipValidateCompress => "Ejecuta una validación adicional después de la compresión";

    // Update
    public override string LatestVersion => "Última versión";
    public override string NoUpdateNotes => "No hay notas de actualización disponibles.";
    public override string UpdateAvailable => "Actualización disponible";
    public override string UpdateDescription => "Se ha publicado una nueva versión de RomForge.";
    public override string UpdateTitle => "Actualización de RomForge";

    public override string StatusWaiting => "En espera";
    public override string StatusConverting => "Convirtiendo";
    public override string StatusDecrypting => "Descifrando";
    public override string StatusExtracting => "Extrayendo";
    public override string StatusDownloading => "Descargando";
    public override string StatusUnzipping => "Descomprimiendo";
    public override string StatusUnzipDone => "Descomprimido";
    public override string StatusUnzipFailed => "Error al descomprimir";
    public override string StatusDone => "Hecho";
    public override string StatusFailed => "Error";
    public override string StatusCancelled => "Cancelado";
    public override string StatusUnsupported => "No compatible";
    public override string StatusSkipped => "Omitido";
    public override string RenameStatusReady => "Se renombrará";
    public override string RenameStatusUnchanged => "Sin cambios";
    public override string RenameStatusMissingInfo => "Sin información";
    public override string RenameStatusEmptyName => "Sin nombre";
    public override string RenameStatusNameTooLong => "Nombre muy largo";
    public override string RenameStatusDuplicate => "Nombre duplicado";
    public override string RenameStatusTargetExists => "El archivo existe";

    public override string ProgressElapsed(object arg0) => $"{arg0} transcurrido";
    public override string ProgressCompletedCount(object arg0, object arg1) => $"{arg0} / {arg1} completado";
    public override string ProgressHashingPercent(object arg0) => $"Calculando hash... {arg0}%";
    public override string ProgressAnalyzingPatchInfo => "Analizando información del parche...";
    public override string ProgressExtractingPatch => "Descomprimiendo parche...";
    public override string ProgressUnpackingFile(object arg0) => $"Desempaquetando: {arg0}";
    public override string ProgressRepackingFile(object arg0) => $"Reempaquetando: {arg0}";
    public override string ProgressApplyingPatchTo(object arg0) => $"Aplicando parche: {arg0}";
    public override string ProgressHashingPartitions(object arg0) => $"Calculando hash de particiones: {arg0}";
    public override string ProgressCompressingWbfs(object arg0) => $"Comprimiendo WBFS: {arg0}";
    public override string ProgressCompressingRvz(object arg0) => $"Comprimiendo RVZ: {arg0}";
    public override string ProgressCreatingIso(object arg0) => $"Creando ISO: {arg0}";
    public override string ProgressWupCreated(object arg0, object arg1) => $"WUP creado: {arg0}_v{arg1}";
    public override string MsgCancelled => "Cancelado.";
    public override string StatusInitializing => "Inicializando...";
    public override string StatusSelectSdAndMovable => "Seleccione la tarjeta SD y movable.sed";
    public override string ProgressLoadingCount(object arg0, object arg1) => $"Cargando... {arg0} / {arg1}";
    public override string ProgressScanningCount(object arg0, object arg1) => $"Escaneando... {arg0} / {arg1}";
    public override string StatusRecoveringTitleDb => "Recuperando title.db...";
    public override string StatusRecoveryDone(object arg0) => $"Recuperación completada: {arg0} títulos";
    public override string StatusExtractingTitle(object? arg0, object arg1) => $"Extrayendo: {arg0} - {arg1}";
    public override string StatusInstallStarted(object? arg0) => $"Instalación iniciada: {arg0}";
    public override string StatusInstallDone(object? arg0) => $"Instalación completada: {arg0}";
    public override string StatusScanningTitles => "Escaneando títulos...";
    public override string StatusLoadingMetadata(object arg0) => $"Cargando metadatos... ({arg0} en total)";
    public override string StatusTitlesLoaded(object arg0) => $"{arg0} títulos cargados";
    public override string ProgressPreparing => "Preparando";
    public override string ProgressHashing => "Calculando hash";
    public override string ProgressCompressingNsz => "Comprimiendo NSZ";

    public override string StatusUnpacking => "Desempaquetando";
    public override string StatusApplyingPatch => "Aplicando parche";
    public override string StatusApplyingPatchAndCompressing => "Aplicando parche y comprimiendo";
    public override string StatusLoadingSource => "Cargando origen...";
    public override string StatusInstalling => "Instalando...";
    public override string StatusExtractingCia => "Extrayendo CIA...";
    public override string ProgressHashingNca => "Calculando hash NCA";
    public override string ProgressCompressingRomfs => "Comprimiendo RomFS";
    public override string ProgressBuildingPfs0 => "Construyendo PFS0";
    public override string ProgressBuildingRomfs => "Construyendo RomFS";
    public override string ProgressWritingRomfs => "Escribiendo RomFS";
    public override string ProgressCreatingIvfc(object arg0, object arg1) => $"Creando IVFC... {arg0}/{arg1}";
    public override string ProgressFinalizingNsp => "Finalizando NSP...";
    public override string ProgressBuildingProgram => "Construyendo programa...";
    public override string ProgressApplyingExefsPatches(object arg0) => $"Aplicando exefs_patches... ({arg0})";
    public override string ProgressApplyingLabel(object arg0) => $"Aplicando {arg0}...";
    public override string LabelDlcPatch(object arg0) => $"Parche de DLC ({arg0})";
    public override string LabelKoreanPatch => "Parche coreano";
    public override string ProgressUnpackingNcaEntry(object arg0, object arg1) => $"Desempaquetando {arg0} - {arg1}";
    public override string ProgressExtractDone => "Extracción completada";
    public override string ProgressCompressDone => "Compresión completada";
    public override string ProgressConvertingBinCue => "Convirtiendo BIN/CUE";
    public override string ProgressConvertingIso => "Convirtiendo ISO";
    public override string ProgressPacking => "Empaquetando";
    public override string ProgressScanningTitle(object arg0) => $"Escaneando: {arg0}";
    public override string ProgressScanError(object arg0, object arg1) => $"Error ({arg0}): {arg1}";
    public override string ProgressExtractingIso => "Extrayendo ISO...";
    public override string ProgressCopyingTrack(object arg0, object arg1) => $"Copiando pista ({arg0}/{arg1})";
    public override string ProgressConvertingChd => "Convirtiendo a CHD...";
    public override string ProgressCompressingZcci => "Comprimiendo ZCCI...";
    public override string ProgressConvertingFormat => "Convirtiendo formato...";
    public override string ProgressPatchDone => "Parche completado";
    public override string ProgressExtractingSource => "Descomprimiendo origen...";
    public override string ProgressGdParsingMetadata => "Analizando metadatos GDI y estructura de origen...";
    public override string ProgressGdPatchAllDone => "¡Todas las tareas de parcheo completadas!";
    public override string ProgressGdExtractingDcp(object arg0) => $"Descomprimiendo paquete DCP: {arg0}";
    public override string ProgressGdApplyingXdelta(object arg0) => $"Aplicando parche delta xdelta: {arg0}";
    public override string ProgressGdPreparingMedia => "Preparando el análisis del medio GD-ROM existente...";
    public override string ProgressApplyingPatchCount(object arg0, object arg1) => $"Aplicando parche ({arg0}/{arg1})";
    public override string ProgressGdRelocatingIso9660(object arg0, object arg1) => $"Reubicando tablas de directorios y estructura ISO9660 ({arg0}/{arg1})";
    public override string ProgressGdMergingSectors => "Iniciando la fusión y ordenación de sectores duplicados...";
    public override string ProgressGdCreatingTrack(object arg0, object arg1) => $"Creando {arg0}: {arg1}";
    public override string ProgressGdAnalyzingTracks => "Analizando las pistas restantes...";
    public override string ProgressGdCopyingTrack(object arg0, object arg1, object arg2) => $"Copiando pista de recursos: {arg0} ({arg1}MB / {arg2}MB)";
    public override string ProgressGdUpdatingIndex => "Verificando y actualizando el archivo de índice GDI...";
    public override string ProgressGdRebuildAllDone => "¡Todas las tareas de reconstrucción de GD-ROM completadas!";
    public override string ProgressGdAligningSectors => "Alineando sectores de la pista de datos...";
    public override string ProgressGdWritingRawSectors => "Generando y escribiendo sectores sin procesar de la pista de datos...";
    public override string ProgressGdTrackWritten => "Pista de datos escrita.";
}