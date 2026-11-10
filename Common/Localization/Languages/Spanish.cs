namespace Common.Localization.Languages;

public sealed class Spanish : LocalizedStrings
{
    public override string Code => "es-ES";
    public override string DisplayName => "Español";
    public override string EnglishName => "Spanish";

    // Button
    public override string ButtonCancel => "Cancelar";
    public override string ButtonSettings => "Configuración";
    public override string ButtonStart => "Iniciar";

    // Dialog
    public override string DialogSelectGameFile => "Seleccionar archivo del juego";

    // Error
    public override string ErrorNoMetadata => "No hay metadatos analizables disponibles.";

    // Filter
    public override string FilterAllFiles => "Todos los archivos";
    public override string FilterSwitchFiles => "Archivos de Switch";

    // Hint
    public override string HintSelectOutput => "Selecciona la carpeta de salida";

    // Header
    public override string HeaderFileName => "Nombre del archivo";
    public override string HeaderFilePath => "Ruta";
    public override string HeaderTitle => "Título";
    public override string HeaderPatch => "Parche";
    public override string HeaderVersion => "Versión";
    public override string HeaderInput => "Entrada";
    public override string HeaderOutput => "Salida";

    // Label
    public override string LabelFile => "Archivo";
    public override string LabelCompression => "Compresión";
    public override string HeaderCompressionRatio => "Relación de compresión";
    public override string LabelOutputFormat => "Formato de salida:";

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
    public override string MenuOpenFolder => "Abrir carpeta";

    // Msg
    public override string MsgTitleError => "Error";
    public override string MsgTitleInfo => "Información";
    public override string MsgTitleQuestion => "Confirmación";
    public override string MsgTitleWarning => "Advertencia";

    // Status
    public override string StatusAnalyzing => "Analizando...";
    public override string StatusNoKey => "Sin clave";

    // ToolTip
    public override string ToolTipAddFile => "Añadir archivo";
    public override string ToolTipAddFolder => "Añadir carpeta";
    public override string ToolTipBlocklessMode => "Acceso secuencial (prioridad de espacio)";
    public override string ToolTipBlockMode => "Acceso aleatorio (prioridad de velocidad)";
    public override string ToolTipOpenWorkSpace => "Abrir carpeta de salida";
    public override string ToolTipClear => "Vaciar";
    public override string ToolTipRemoveSelected => "Eliminar";
    public override string ToolTipValidateCompress => "Ejecuta una validación adicional después de la compresión";
    public override string ButtonAddFile => "Añadir archivo";
    public override string ButtonAddFolder => "Añadir carpeta";
    public override string ButtonRemoveSelected => "Eliminar";
    public override string ButtonClear => "Vaciar";
    public override string MenuRemoveSelected => "Eliminar";
    public override string MenuClear => "Vaciar";
    public override string TabPatch => "Parche";
    public override string LabelPatch => "Parche";
    public override string TabSettings => "Configuración";
    public override string LabelSettings => "Configuración";
    public override string LabelVersion => "Versión";
    public override string HintDropCueChd => "Arrastra y suelta un archivo CUE/CHD";
    public override string HintDropSourceNsp => "Arrastra y suelta el archivo NSP/NSZ de origen";
    public override string HintDragOrSelectWorkFolder => "Arrastra o selecciona la carpeta de trabajo";
    public override string HintDragOrSelectOutputFolder => "Arrastra o selecciona la carpeta de salida";
    public override string HintDropImageFile => "Arrastra y suelta un archivo de imagen";
    public override string HintDropFileOrFolder => "Arrastra y suelta archivos o carpetas aquí";
    public override string HintDropGameFileOrFolder => "Arrastra y suelta archivos/carpetas de juego aquí";
    public override string HintRepackReplaceRule => "El juego base y la actualización se reemplazan automáticamente; los DLC se añaden";
    public override string HintSplitExpandRule => "Despliega una ROM combinada con el botón + para elegir el juego base, la actualización o los DLC que quieres dividir";
    public override string LabelGameName => "Nombre del juego";
    public override string LabelPublisher => "Editor";
    public override string LabelGameId => "ID del juego";
    public override string LabelNspTitleId => "Title ID del NSP";
    public override string LabelWideScreen => "Pantalla panorámica";
    public override string LabelSoundVolume => "Volumen de sonido:";
    public override string LabelLz4Compression => "Compresión Lz4";
    public override string LabelSupportedLanguages => "Idiomas compatibles";
    public override string LabelMultiRomIndex => "Índice multi-ROM";
    public override string LabelMergeMode => "Modo de fusión:";
    public override string LabelEstimated => "Estimado";
    public override string LabelActual => "Real";
    public override string LabelFileCount(object arg0) => $"{arg0} archivos";
    public override string HeaderFileNameCompare => "Comparación de nombres (actual/nuevo)";
    public override string DescConvertFormats => "Convierte entre los siguientes formatos de archivo";
    public override string DescRenameByFormat => "Cambia el nombre de los archivos por lotes según el formato seleccionado";
    public override string ButtonForceLanguage => "Forzar idioma";
    public override string ButtonCancelForceLanguage => "Cancelar idioma forzado";
    public override string ButtonUnpack => "Desempaquetar";
    public override string ButtonRepackFolderBased => "Reempaquetar (basado en carpeta)";
    public override string ButtonUnpackAndRepackStreaming => "Desempaquetar + Reempaquetar (basado en streaming)";
    public override string TabRepack => "Reempaquetar";
    public override string TabMerge => "Fusionar";
    public override string TabSplit => "Dividir";
    public override string TabLz4Compression => "Compresión Lz4";
    public override string TabFormatConvert => "Conversión de formato";
    public override string TabSaturnNspConvert => "Conversión NSP de Saturn";
    public override string TabIgnoreVersion => "Ignorar versión";
    public override string TabRename => "Renombrar archivos";
    public override string RadioMergeGroupByTitleId => "Agrupar por Title ID (método anterior)";
    public override string RadioMergeAll => "Todo";
    public override string MenuSelectFolder => "Seleccionar carpeta...";
    public override string MenuSelectArchive => "Seleccionar archivo comprimido (zip/7z)...";
    public override string MenuDeselect => "Anular selección";
    public override string MenuFindInFolder => "Buscar en carpeta...";
    public override string MenuFindInArchive => "Buscar en archivo comprimido (zip/7z)...";
    public override string ToolTipTitleIdInput => "Número hexadecimal de 16 dígitos (p. ej.: 0100000000010000). Déjalo vacío para conservar el Title ID original.";
    public override string ToolTipForceLanguage => "Al reempaquetar, solo se activan los idiomas seleccionados y se desactivan todos los demás.";
    public override string ToolTipMergeGroupByTitleId => "Agrupa los elementos por Title ID y fusiona cada grupo en un archivo independiente.";
    public override string ToolTipMergeAll => "Fusiona todos los elementos en un solo archivo.";
    public override string ToolTipRomFsLz4 => "Compresión Lz4 de RomFS (se aplica al reempaquetar). El juego base y la actualización se combinan en uno, por lo que marcar cualquiera de los dos activa la compresión.";
    public override string ToolTipBatchKoreanPatch => "Asignar parches de coreano por lotes (carpeta raíz o archivo comprimido)";
    public override string ToolTipSelectAllToggle => "Seleccionar/deseleccionar todo";
    public override string ToolTipExpandCollapse => "Expandir/Contraer";
    public override string HeaderSize => "Tamaño";
    public override string ToolTipBrowseOutput => "Seleccionar carpeta de salida";
    public override string HeaderConvert => "Conversión";
    public override string TabGeneral => "General";
    public override string TabArcade => "Arcade";
    public override string TabConvert => "Conversión";
    public override string TabUtil => "Utilidades";
    public override string ButtonReset => "Restablecer";
    public override string ButtonCalculateHash => "Calcular hash";
    public override string ButtonOk => "Aceptar";
    public override string ToggleChangeOutputFolder => "Cambiar carpeta de salida";
    public override string ToggleAutoCompressAfterPatch => "Comprimir automáticamente tras el parche";
    public override string ToggleAutoVersionDateInName => "Añadir automáticamente versión/fecha al nombre del archivo resultante";
    public override string MenuSelectFileIpsZip => "Seleccionar archivo (*.ips, *.zip)...";
    public override string MenuClearLog => "Borrar registro";
    public override string MenuCopyLog => "Copiar registro";
    public override string ToolTipSelectFile => "Seleccionar archivo";
    public override string ToolTipRestoreDefaultPath => "Restaurar ubicación predeterminada";
    public override string ToolTipChangePatchPackage => "Cambiar a otro paquete de parches";
    public override string ToolTipChangePatchFile => "Cambiar a otro archivo de parche";
    public override string ToolTipAddSequentialPatch => "Añadir un parche para aplicar en secuencia";
    public override string ToolTipOpenGithubReleases => "Abre la página de versiones de GitHub";
    public override string ToolTipOpenHelp => "Abre la página de ayuda";
    public override string LabelSource => "Origen";
    public override string LabelSelectPatch => "Seleccionar parche:";
    public override string LabelOthers => "Otros";
    public override string LabelExpectedFileName => "Nombre de archivo previsto:";
    public override string LabelSupportedPatchFormats => "Formatos de parche compatibles:";
    public override string LabelSourceAutoExtractFormats => "Formatos compatibles con la extracción automática del origen:";
    public override string LabelFormatListIsoStandard => "ISO (estándar), CUE+BIN, GDI+BIN+RAW, CCD+IMG+SUB";
    public override string LabelFormatList3dsCciCia => "3DS (mismo formato que CCI), CCI, CIA";
    public override string LabelFormat3dsCci => "3DS (mismo formato que CCI)";
    public override string LabelFormatChdIsoBased => "CHD (basado en ISO)";
    public override string LabelFormatBinCueSingleTrack => "BIN+CUE (pista única)";
    public override string DescAutoCompressByRule => "El formato se selecciona automáticamente según las reglas siguientes y se comprime o descomprime";
    public override string DescConvertByRule => "La compresión, descompresión y conversión de formato se realizan según las reglas siguientes";
    public override string DescAutoMatchByExtension => "La coincidencia se realiza automáticamente según la extensión";
    public override string DescAutoVersionDateInName => "Lee la versión y la fecha del nombre del archivo de parche y las muestra en el nombre del archivo resultante";
    public override string DescPc98Hdi => "Busca y reemplaza archivos internos del HDI (FAT12/16) por ruta lógica. La ruta de montaje se detecta automáticamente.";
    public override string DescArchivePasswordProtected => "Este archivo comprimido está protegido con contraseña.";
    public override string HintDropPatchFile => "Arrastra y suelta un archivo de parche";
    public override string HintDropArcadeFiles => "Arrastra y suelta el ZIP de origen y los parches (IPS, carpeta, ZIP) arriba";
    public override string StatusMatched => "Emparejado";
    public override string StatusUnmatched => "Sin emparejar";
    public override string StatusHashCancelledByPatch => "El cálculo del hash se canceló porque se inició el parche";
    public override string StatusHashFailed => "Error al calcular el hash";
    public override string StatusWrongPassword => "La contraseña es incorrecta.";
    public override string TitleArchivePassword => "Contraseña del archivo comprimido";
}