namespace Common.Localization.Languages;

public sealed class German : LocalizedStrings
{
    public override string Code => "de-DE";
    public override string DisplayName => "Deutsch";
    public override string EnglishName => "German";

    // Button
    public override string ButtonCancel => "Abbrechen";
    public override string ButtonSettings => "Einstellungen";
    public override string ButtonStart => "Starten";

    // Dialog
    public override string DialogSelectGameFile => "Spieldatei auswählen";

    // Error
    public override string ErrorNoMetadata => "Keine analysierbaren Metadaten vorhanden.";

    // Filter
    public override string FilterAllFiles => "Alle Dateien";
    public override string FilterSwitchFiles => "Switch-Dateien";

    // Hint
    public override string HintSelectOutput => "Wählen Sie den Ausgabeordner aus.";

    // Header
    public override string HeaderFileName => "Dateiname";
    public override string HeaderFilePath => "Pfad";
    public override string HeaderTitle => "Titel";
    public override string HeaderPatch => "Patch";
    public override string HeaderVersion => "Version";
    public override string HeaderInput => "Eingabe";
    public override string HeaderOutput => "Ausgabe";

    // Label
    public override string LabelFile => "Datei";
    public override string LabelCompression => "Komprimierung";
    public override string LabelCompressionRatio => "Kompressionsrate";
    public override string LabelOutputFormat => "Ausgabeformat:";

    // Log
    public override string LogAnalyzeMetadata => "━━ Metadaten werden analysiert... ━━";
    public override string LogCompressAndMerge => "Komprimieren & Zusammenführen...";
    public override string LogCompressAndSplit => "Komprimieren & Aufteilen...";
    public override string LogCompressionRatio => "Kompressionsrate";
    public override string LogDecompressAndMerge => "Dekomprimieren und Zusammenführen";
    public override string LogDeleteIncompleteFile => "Unvollständig erstellte Datei wurde gelöscht.";
    public override string LogError(object arg0) => $"Fehler: {arg0}";
    public override string LogFinalId(object arg0, object arg1) => $"Endgültige ID: {arg0} / Version: {arg1}";
    public override string LogMergeComplete(object arg0) => $"Merge abgeschlossen: {arg0}";
    public override string LogMergeFailed(object? arg0, object arg1) => $"[{arg0}] Zusammenführung fehlgeschlagen: {arg1}";
    public override string LogMerging => "Zusammenführen...";
    public override string LogModeCompress => "Komprimieren";
    public override string LogModeDecompress => "Dekomprimieren";
    public override string LogProcessError => "Fehler bei Verarbeitung";
    public override string LogProcessStart => "Start";
    public override string LogSplitComplete(object arg0) => $"Split abgeschlossen: {arg0}";
    public override string LogSplitFailed(object arg0, object arg1) => $"Split fehlgeschlagen (ID: {arg0}): {arg1}";
    public override string LogSplitPreparing(object arg0) => $"{arg0} Aufteilen wird gestartet";
    public override string LogSplitting => "Trennen...";
    public override string LogStatusCompressing => "Komprimiere...";
    public override string LogStatusCopying => "Kopieren...";
    public override string LogStatusDecompressing => "Dekomprimiere...";
    public override string LogStatusDone => "Fertig";
    public override string LogStatusValidating => "Validierung...";
    public override string LogTitleGroupDetected(object arg0) => $"{arg0} Titelgruppen erkannt";
    public override string LogValidationComplete => "Validierung abgeschlossen";
    public override string LogValidationStart => "Validierung wird gestartet";

    // Main
    public override string MainErrNoFiles => "Bitte fügen Sie Dateien hinzu und versuchen Sie es erneut.";
    public override string MainErrNoKeys => "Die Datei „prod.keys“ wurde nicht gefunden. Bitte legen Sie die Schlüsseldatei im Anwendungsordner ab.";
    public override string MainErrNoOutput => "Bitte legen Sie den Ausgabeordner fest.";
    public override string MainLogAllComplete(object arg0) => $"✓ Alle Aufgaben abgeschlossen. Gesamtzeit: {arg0}";
    public override string MainLogRecalculating => "Dateitypen werden neu analysiert...";
    public override string MainMsgDone => "Fertig!";
    public override string MainMsgSplitDone => "Split abgeschlossen.";

    // Menu
    public override string MenuOpenFolder => "Ordner öffnen";

    // Msg
    public override string MsgTitleError => "Fehler";
    public override string MsgTitleInfo => "Information";
    public override string MsgTitleQuestion => "Bestätigung";
    public override string MsgTitleWarning => "Warnung";

    // Status
    public override string StatusAnalyzing => "Analyse läuft...";
    public override string StatusNoKey => "Kein Schlüssel";

    // ToolTip
    public override string ToolTipAddFile => "Datei+";
    public override string ToolTipAddFolder => "Ordner+";
    public override string ToolTipBlocklessMode => "Sequentieller Zugriff (Speicher priorisiert)\t";
    public override string ToolTipBlockMode => "Zufälliger Zugriff (Geschwindigkeit priorisiert)";
    public override string ToolTipOpenWorkSpace => "Ausgabeordner öffnen";
    public override string ToolTipRemoveAll => "Leeren";
    public override string ToolTipRemoveSelected => "Löschen";
    public override string ToolTipValidateCompress => "Führt nach der Komprimierung eine zusätzliche Validierung durch";
}