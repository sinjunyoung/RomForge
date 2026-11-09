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
    public override string HintSelectOutput => "Seleccionar carpeta de salida...";

    // Header
    public override string HeaderFileName => "Nombre del archivo";
    public override string HeaderFilePath => "Ruta";

    // Label
    public override string LabelCompression => "Compresión";
    public override string LabelCompressionRatio => "Relación de compresión";

    // Log
    public override string LogAnalyzeMetadata => "━━ Analizando metadatos... ━━";
    public override string LogBaseMissingSkip(object arg0) => $"[{arg0}] Falta la base (juego principal) – omitido";
    public override string LogCompressAndMerge => "Comprimiendo y fusionando...";
    public override string LogCompressAndSplit => "Comprimiendo y dividiendo...";
    public override string LogCompressionRatio => "Relación de compresión";
    public override string LogDecompressAndMerge => "Descomprimiendo y fusionando";
    public override string LogDecompressAndSplit => "Descomprimiendo y dividiendo";
    public override string LogDeleteIncompleteFile => "Se eliminó el archivo creado de forma incompleta.";
    public override string LogError(object arg0) => $"Ocurrió un error: {arg0}";
    public override string LogFinalId(object arg0, object arg1) => $"ID final: {arg0} / Versión: {arg1}";
    public override string LogMergeComplete(object arg0) => $"Combinación completada: {arg0}";
    public override string LogMergeFailed(object? arg0, object arg1) => $"[{arg0}] Error al fusionar: {arg1}";
    public override string LogMergeStart => "━━ Iniciando combinación Super NSP ━━";
    public override string LogMerging => "Fusionando...";
    public override string LogModeCompress => "Comprimir";
    public override string LogModeDecompress => "Descomprimir";
    public override string LogProcessError => "Error de procesamiento";
    public override string LogProcessStart => "Iniciar";
    public override string LogSplitComplete(object arg0) => $"dividido correctamente: {arg0}";
    public override string LogSplitDecompressing(object arg0) => $"- {arg0} Descomprimiendo y dividiendo...";
    public override string LogSplitExtracting(object arg0) => $"- {arg0} Dividiendo...";
    public override string LogSplitFailed(object arg0, object arg1) => $"Error al dividir (ID: {arg0}): {arg1}";
    public override string LogSplitNoTarget => "Ya es contenido único o no hay nada que dividir.";
    public override string LogSplitPreparing(object arg0) => $"{arg0} Iniciando división";
    public override string LogSplitting => "Separando...";
    public override string LogStatusCompressing => "Comprimiendo...";
    public override string LogStatusCopying => "Copiando...";
    public override string LogStatusDecompressing => "Descomprimiendo...";
    public override string LogStatusDone => "Hecho";
    public override string LogStatusValidating => "Validando...";
    public override string LogTitleGroupDetected(object arg0) => $"Se detectaron {arg0} grupos de títulos";
    public override string LogValidating => "Validando...";
    public override string LogValidationComplete => "Validación completada";
    public override string LogValidationStart => "Iniciando validación";
    public override string LogWriteNspFailed(object arg0) => $"Error al escribir NSP: {arg0}";

    // Main
    public override string MainCompressTitle => "Herramienta de compresión NSW";
    public override string MainErrNoBase => "Agrega un archivo que contenga el juego base.";
    public override string MainErrNoFiles => "Agrega archivos y vuelve a intentarlo.";
    public override string MainErrNoKeys => "No se encontró el archivo prod.keys. Coloque el archivo de claves en la carpeta de la aplicación.";
    public override string MainErrNoKeysAndroid => "No se encontró el archivo prod.keys. Coloque el archivo de claves en la carpeta Descargas.";
    public override string MainErrNoOutput => "Configura la carpeta de trabajo.";
    public override string MainLogAllComplete(object arg0) => $"✓ Todas las tareas completadas. Tiempo total: {arg0}";
    public override string MainLogAllSplitDone(object arg0) => $"✓ ¡Todas las divisiones completadas! Tiempo total: {arg0}";
    public override string MainLogElapsed(object arg0) => $"Tiempo transcurrido: {arg0}";
    public override string MainLogRecalculating => "Reanalizando tipos de archivo...";
    public override string MainLogSplitStart(object arg0) => $"━━ Iniciando análisis de división: {arg0} ━━";
    public override string MainMsgDone => "¡Completado!";
    public override string MainMsgSplitDone => "División completada.";
    public override string MainSelectFolder => "Seleccionar carpeta de salida";
    public override string MainTitle => "Herramienta de combinación NSW";

    // Menu
    public override string MenuOpenFolder => "Abrir carpeta";

    // Msg
    public override string MsgTitleError => "Error";
    public override string MsgTitleInfo => "Información";
    public override string MsgTitleQuestion => "Confirmación";
    public override string MsgTitleWarning => "Advertencia";

    // Settings
    public override string SettingsCompressMode => "Modo de compresión";
    public override string SettingsForceKeyGen0 => "Forzar KeyGen 0";
    public override string SettingsTabGeneral => "General";

    // Status
    public override string StatusAnalyzing => "Analizando...";
    public override string StatusNoKey => "Sin clave";
    public override string StatusProcessing => "Procesando...";

    // ToolTip
    public override string ToolTipAddFile => "Añadir archivo";
    public override string ToolTipAddFolder => "Añadir carpeta";
    public override string ToolTipBlocklessMode => "Acceso secuencial (prioridad de espacio)";
    public override string ToolTipBlockMode => "Acceso aleatorio (prioridad de velocidad)";
    public override string ToolTipBrowseOutput => "Examinar carpeta de salida";
    public override string ToolTipCompress => "Comprimir a NSZ al marcar (solo fusión)";
    public override string ToolTipCompressLevel => "3: Más rápido | 9: Equilibrado | 18: Recomendado";
    public override string ToolTipForceKeyGen0 => "Forzar Firmware KeyGen a 0";
    public override string ToolTipOpenWorkSpace => "Abrir carpeta de salida";
    public override string ToolTipRemoveAll => "Vaciar";
    public override string ToolTipRemoveSelected => "Eliminar";
    public override string ToolTipValidateCompress => "Ejecuta una validación adicional después de la compresión";
}