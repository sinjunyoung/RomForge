namespace Common.Localization.Languages;

public sealed class French : LocalizedStrings
{
    public override string Code => "fr-FR";
    public override string DisplayName => "Français";

    // Button
    public override string ButtonCancel => "Annuler";
    public override string ButtonSettings => "Paramètres";
    public override string ButtonStart => "Démarrer";

    // Dialog
    public override string DialogSelectGameFile => "Sélectionner le fichier de jeu";

    // Error
    public override string ErrorNoCompressFiles => "Aucun fichier à compresser.";
    public override string ErrorNoDecompressFiles => "Aucun fichier à décompresser.";
    public override string ErrorNoMetadata => "Aucune métadonnée analysable disponible.";

    // Filter
    public override string FilterAllFiles => "Tous les fichiers";
    public override string FilterSwitchFiles => "Fichiers Switch";

    // Hint
    public override string HintDragAndDrop => "Glissez-déposez les fichiers NSP, NSZ, XCI, XCZ ici";
    public override string HintSelectOutput => "Sélectionner le dossier de sortie...";

    // Header
    public override string HeaderExt => "Ext";
    public override string HeaderFileName => "Nom du fichier";
    public override string HeaderFilePath => "Chemin";
    public override string HeaderSize => "Taille";
    public override string HeaderType => "Type";

    // Label
    public override string LabelCompression => "Compression";
    public override string LabelOutputPath => "Dossier de sortie";

    // Log
    public override string LogAnalyzeMetadata => "━━ Analyse des métadonnées...";
    public override string LogBaseMissingSkip(object arg0) => $"[{arg0}] Base (jeu principal) manquante – ignoré";
    public override string LogCompressAndMerge => "Compression et fusion en cours...";
    public override string LogCompressAndSplit => "Compression et fractionnement en cours...";
    public override string LogCompressionRatio => "Taux de compression";
    public override string LogDecompressAndMerge => "Décompression et fusion";
    public override string LogDecompressAndSplit => "Décompression et division";
    public override string LogDeleteIncompleteFile => "Fichier incomplet supprimé.";
    public override string LogError(object arg0) => $"Une erreur est survenue : {arg0}";
    public override string LogFinalId(object arg0, object arg1) => $"ID final : {arg0} / Version : {arg1}";
    public override string LogMergeComplete(object arg0) => $"Fusion terminée : {arg0}";
    public override string LogMergeFailed(object? arg0, object arg1) => $"[{arg0}] Échec de la fusion : {arg1}";
    public override string LogMergeStart => "━━ Démarrage de la fusion Super NSP ━━";
    public override string LogMerging => "Fusion en cours...";
    public override string LogModeCompress => "Compression";
    public override string LogModeDecompress => "Decompression";
    public override string LogProcessError => "Erreur de traitement";
    public override string LogProcessStart => "Demarrer";
    public override string LogSplitComplete(object arg0) => $"division terminée: {arg0}";
    public override string LogSplitDecompressing(object arg0) => $"- {arg0} Décompression et division...";
    public override string LogSplitExtracting(object arg0) => $"- {arg0} Division...";
    public override string LogSplitFailed(object arg0, object arg1) => $"Échec de la division (ID : {arg0}) : {arg1}";
    public override string LogSplitNoTarget => "Déjà un contenu unique ou aucune cible à diviser.";
    public override string LogSplitPreparing(object arg0) => $"{arg0} Début de la division ";
    public override string LogSplitting => "Separation en cours...";
    public override string LogStatusCompressing => "Compression en cours...";
    public override string LogStatusCopying => "Copie en cours...";
    public override string LogStatusDecompressing => "Decompression...";
    public override string LogStatusDone => "Termine";
    public override string LogStatusValidating => "Validation...";
    public override string LogTitleGroupDetected(object arg0) => $"{arg0} groupes de titres détectés";
    public override string LogValidating => "Validation en cours...";
    public override string LogValidationComplete => "Validation terminée";
    public override string LogValidationStart => "Démarrage de la validation";
    public override string LogWriteNspFailed(object arg0) => $"Échec de l'écriture du NSP : {arg0}";

    // Main
    public override string MainCompressTitle => "Outil de compression NSW";
    public override string MainErrNoBase => "Veuillez ajouter un fichier contenant le jeu de base.";
    public override string MainErrNoFiles => "Veuillez ajouter des fichiers et réessayer.";
    public override string MainErrNoKeys => "Fichier prod.keys introuvable. Veuillez placer le fichier de clés dans le dossier de l’application.";
    public override string MainErrNoKeysAndroid => "Fichier prod.keys introuvable. Veuillez placer le fichier de clés dans le dossier Téléchargements.";
    public override string MainErrNoOutput => "Veuillez définir le dossier de travail.";
    public override string MainLogAllComplete(object arg0) => $"✓ Toutes les tâches terminées. Temps total : {arg0}";
    public override string MainLogAllSplitDone(object arg0) => $"✓ Toutes les divisions terminées ! Temps total : {arg0}";
    public override string MainLogElapsed(object arg0) => $"Temps écoulé : {arg0}";
    public override string MainLogRecalculating => "Réanalyse des types de fichiers...";
    public override string MainLogSplitStart(object arg0) => $"━━ Démarrage de l'analyse de division : {arg0} ━━";
    public override string MainMsgDone => "Terminé!";
    public override string MainMsgSplitDone => "Division terminée.";
    public override string MainSelectFolder => "Sélectionner le dossier de travail";
    public override string MainTitle => "Outil de fusion NSW";

    // Menu
    public override string MenuOpenFolder => "Ouvrir le dossier";

    // Msg
    public override string MsgTitleError => "Erreur";
    public override string MsgTitleInfo => "Information";
    public override string MsgTitleQuestion => "Confirmation";
    public override string MsgTitleWarning => "Avertissement";

    // Settings
    public override string SettingsCompressMode => "Mode de compression";
    public override string SettingsForceKeyGen0 => "Forcer KeyGen 0";
    public override string SettingsTabGeneral => "Général";

    // Status
    public override string StatusAnalyzing => "Analyse en cours...";
    public override string StatusNoKey => "Aucune clé";
    public override string StatusProcessing => "Traitement en cours...";

    // ToolTip
    public override string ToolTipAddFile => "Ajouter un fichier";
    public override string ToolTipBlocklessMode => "Accès séquentiel (priorité espace)";
    public override string ToolTipBlockMode => "Accès aléatoire (priorité vitesse)";
    public override string ToolTipBrowseOutput => "Parcourir le dossier de sortie";
    public override string ToolTipCompress => "Comprimir a NSZ al marcar (solo fusión)";
    public override string ToolTipCompressLevel => "3 : Le plus rapide | 9 : Équilibré | 18 : Recommandé";
    public override string ToolTipForceKeyGen0 => "Forcer le Firmware KeyGen à 0";
    public override string ToolTipOpenWorkSpace => "Ouvrir le dossier de sortie";
    public override string ToolTipRemoveAll => "Tout supprimer";
    public override string ToolTipRemoveSelected => "Supprimer la sélection";
    public override string ToolTipValidateCompress => "Exécute une validation supplémentaire après la compression";
}