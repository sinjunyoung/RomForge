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
    public override string HintSelectOutput => "Wählen Sie den Ausgabeordner aus";

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
    public override string HeaderCompressionRatio => "Kompressionsrate";
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
    public override string LogMergeComplete(object arg0) => $"Zusammenführung abgeschlossen: {arg0}";
    public override string LogMergeFailed(object? arg0, object arg1) => $"[{arg0}] Zusammenführung fehlgeschlagen: {arg1}";
    public override string LogMerging => "Zusammenführen...";
    public override string LogModeCompress => "Komprimieren";
    public override string LogModeDecompress => "Dekomprimieren";
    public override string LogProcessError => "Fehler bei der Verarbeitung";
    public override string LogProcessStart => "Start";
    public override string LogSplitComplete(object arg0) => $"Aufteilung abgeschlossen: {arg0}";
    public override string LogSplitFailed(object arg0, object arg1) => $"Aufteilung fehlgeschlagen (ID: {arg0}): {arg1}";
    public override string LogSplitPreparing(object arg0) => $"{arg0} Aufteilen wird gestartet";
    public override string LogSplitting => "Aufteilen...";
    public override string LogStatusCompressing => "Komprimieren...";
    public override string LogStatusCopying => "Kopieren...";
    public override string LogStatusDecompressing => "Dekomprimieren...";
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
    public override string MainMsgSplitDone => "Aufteilung abgeschlossen.";

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
    public override string ToolTipAddFile => "Datei hinzufügen";
    public override string ToolTipAddFolder => "Ordner hinzufügen";
    public override string ToolTipBlocklessMode => "Sequentieller Zugriff (Speicher priorisiert)";
    public override string ToolTipBlockMode => "Zufälliger Zugriff (Geschwindigkeit priorisiert)";
    public override string ToolTipOpenWorkSpace => "Ausgabeordner öffnen";
    public override string ToolTipClear => "Leeren";
    public override string ToolTipRemoveSelected => "Löschen";
    public override string ToolTipValidateCompress => "Führt nach der Komprimierung eine zusätzliche Validierung durch";
    public override string ButtonAddFile => "Datei hinzufügen";
    public override string ButtonAddFolder => "Ordner hinzufügen";
    public override string ButtonRemoveSelected => "Löschen";
    public override string ButtonClear => "Leeren";
    public override string MenuRemoveSelected => "Löschen";
    public override string MenuClear => "Leeren";
    public override string TabPatch => "Patch";
    public override string LabelPatch => "Patch";
    public override string TabSettings => "Einstellungen";
    public override string LabelSettings => "Einstellungen";
    public override string LabelVersion => "Version";
    public override string HintDropCueChd => "CUE/CHD-Datei hierher ziehen und ablegen";
    public override string HintDropSourceNsp => "Original-NSP/NSZ-Datei hierher ziehen und ablegen";
    public override string HintDragOrSelectWorkFolder => "Arbeitsordner ziehen oder auswählen";
    public override string HintDragOrSelectOutputFolder => "Ausgabeordner ziehen oder auswählen";
    public override string HintDropImageFile => "Bilddatei hierher ziehen und ablegen";
    public override string HintDropFileOrFolder => "Dateien oder Ordner hierher ziehen und ablegen";
    public override string HintDropGameFileOrFolder => "Spieldateien/-ordner hierher ziehen und ablegen";
    public override string HintRepackReplaceRule => "Hauptspiel/Update werden automatisch ersetzt, DLC wird hinzugefügt";
    public override string HintSplitExpandRule => "Kombinierte ROMs mit der +-Taste aufklappen, um Hauptspiel/Update/DLC zum Aufteilen auszuwählen";
    public override string LabelGameName => "Spielname";
    public override string LabelPublisher => "Publisher";
    public override string LabelGameId => "Spiel-ID";
    public override string LabelNspTitleId => "NSP-Title-ID";
    public override string LabelWideScreen => "Breitbild";
    public override string LabelSoundVolume => "Lautstärke:";
    public override string LabelLz4Compression => "Lz4-Komprimierung";
    public override string LabelSupportedLanguages => "Unterstützte Sprachen";
    public override string LabelMultiRomIndex => "Multi-ROM-Index";
    public override string LabelMergeMode => "Zusammenführungsmodus:";
    public override string LabelEstimated => "Geschätzt";
    public override string LabelActual => "Tatsächlich";
    public override string LabelFileCount(object arg0) => $"{arg0} Dateien";
    public override string HeaderFileNameCompare => "Dateinamenvergleich (aktuell/neu)";
    public override string DescConvertFormats => "Konvertiert zwischen den folgenden Dateiformaten";
    public override string DescRenameByFormat => "Benennt Dateien gesammelt nach dem gewählten Format um";
    public override string ButtonForceLanguage => "Sprache erzwingen";
    public override string ButtonCancelForceLanguage => "Erzwungene Sprache aufheben";
    public override string ButtonUnpack => "Entpacken";
    public override string ButtonRepackFolderBased => "Repack (ordnerbasiert)";
    public override string ButtonUnpackAndRepackStreaming => "Entpacken + Repack (streamingbasiert)";
    public override string TabRepack => "Repack";
    public override string TabMerge => "Zusammenführen";
    public override string TabSplit => "Aufteilen";
    public override string TabLz4Compression => "Lz4-Komprimierung";
    public override string TabFormatConvert => "Formatkonvertierung";
    public override string TabSaturnNspConvert => "Saturn-NSP-Konvertierung";
    public override string TabIgnoreVersion => "Version ignorieren";
    public override string TabRename => "Dateien umbenennen";
    public override string RadioMergeGroupByTitleId => "Nach Title ID gruppieren (bisherige Methode)";
    public override string RadioMergeAll => "Alle";
    public override string MenuSelectFolder => "Ordner auswählen...";
    public override string MenuSelectArchive => "Archiv auswählen (zip/7z)...";
    public override string MenuDeselect => "Auswahl aufheben";
    public override string MenuFindInFolder => "Im Ordner suchen...";
    public override string MenuFindInArchive => "Im Archiv suchen (zip/7z)...";
    public override string ToolTipTitleIdInput => "16-stellige Hexadezimalzahl (z. B. 0100000000010000). Leer lassen, um die ursprüngliche Title ID beizubehalten.";
    public override string ToolTipForceLanguage => "Beim Repack werden nur die ausgewählten Sprachen aktiviert, alle anderen deaktiviert.";
    public override string ToolTipMergeGroupByTitleId => "Gruppiert Einträge nach Title ID und führt jede Gruppe in einer eigenen Datei zusammen.";
    public override string ToolTipMergeAll => "Führt alle Einträge in einer einzigen Datei zusammen.";
    public override string ToolTipRomFsLz4 => "RomFS-Lz4-Komprimierung (beim Repack angewendet). Hauptspiel und Update werden zusammengeführt, daher wird komprimiert, sobald eines von beiden angehakt ist.";
    public override string ToolTipBatchKoreanPatch => "Koreanische Patches gesammelt zuweisen (Stammordner oder Archiv)";
    public override string ToolTipSelectAllToggle => "Alle aus-/abwählen";
    public override string ToolTipExpandCollapse => "Aufklappen/Zuklappen";
    public override string HeaderSize => "Größe";
    public override string ToolTipBrowseOutput => "Ausgabeordner auswählen";
}