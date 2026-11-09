namespace Common.Localization.Languages;

public sealed class French : LocalizedStrings
{
    public override string Code => "fr-FR";
    public override string DisplayName => "Français";
    public override string EnglishName => "French";

    // Button
    public override string ButtonCancel => "Annuler";
    public override string ButtonSettings => "Paramètres";
    public override string ButtonStart => "Démarrer";

    // Dialog
    public override string DialogSelectGameFile => "Sélectionner le fichier de jeu";

    // Error
    public override string ErrorNoMetadata => "Aucune métadonnée analysable disponible.";

    // Filter
    public override string FilterAllFiles => "Tous les fichiers";
    public override string FilterSwitchFiles => "Fichiers Switch";

    // Hint
    public override string HintSelectOutput => "Sélectionner le dossier de sortie...";

    // Header
    public override string HeaderFileName => "Nom du fichier";
    public override string HeaderFilePath => "Chemin";
    public override string HeaderTitle => "Titre";
    public override string HeaderPatch => "Patch";
    public override string HeaderVersion => "Version";
    public override string HeaderInput => "Entrée";
    public override string HeaderOutput => "Sortie";

    // Label
    public override string LabelFile => "Fichier";
    public override string LabelCompression => "Compression";
    public override string LabelCompressionRatio => "Taux de compression";
    public override string LabelOutputFormat => "Format de sortie:";

    // Log
    public override string LogAnalyzeMetadata => "━━ Analyse des métadonnées...";
    public override string LogCompressAndMerge => "Compression et fusion en cours...";
    public override string LogCompressAndSplit => "Compression et fractionnement en cours...";
    public override string LogCompressionRatio => "Taux de compression";
    public override string LogDecompressAndMerge => "Décompression et fusion";
    public override string LogDeleteIncompleteFile => "Fichier incomplet supprimé.";
    public override string LogError(object arg0) => $"Une erreur est survenue : {arg0}";
    public override string LogFinalId(object arg0, object arg1) => $"ID final : {arg0} / Version : {arg1}";
    public override string LogMergeComplete(object arg0) => $"Fusion terminée : {arg0}";
    public override string LogMergeFailed(object? arg0, object arg1) => $"[{arg0}] Échec de la fusion : {arg1}";
    public override string LogMerging => "Fusion en cours...";
    public override string LogModeCompress => "Compression";
    public override string LogModeDecompress => "Decompression";
    public override string LogProcessError => "Erreur de traitement";
    public override string LogProcessStart => "Demarrer";
    public override string LogSplitComplete(object arg0) => $"division terminée: {arg0}";
    public override string LogSplitFailed(object arg0, object arg1) => $"Échec de la division (ID : {arg0}) : {arg1}";
    public override string LogSplitPreparing(object arg0) => $"{arg0} Début de la division ";
    public override string LogSplitting => "Separation en cours...";
    public override string LogStatusCompressing => "Compression en cours...";
    public override string LogStatusCopying => "Copie en cours...";
    public override string LogStatusDecompressing => "Decompression...";
    public override string LogStatusDone => "Termine";
    public override string LogStatusValidating => "Validation...";
    public override string LogTitleGroupDetected(object arg0) => $"{arg0} groupes de titres détectés";
    public override string LogValidationComplete => "Validation terminée";
    public override string LogValidationStart => "Démarrage de la validation";

    // Main
    public override string MainErrNoFiles => "Veuillez ajouter des fichiers et réessayer.";
    public override string MainErrNoKeys => "Fichier prod.keys introuvable. Veuillez placer le fichier de clés dans le dossier de l’application.";
    public override string MainErrNoOutput => "Veuillez définir le dossier de travail.";
    public override string MainLogAllComplete(object arg0) => $"✓ Toutes les tâches terminées. Temps total : {arg0}";
    public override string MainLogRecalculating => "Réanalyse des types de fichiers...";
    public override string MainMsgDone => "Terminé!";
    public override string MainMsgSplitDone => "Division terminée.";

    // Menu
    public override string MenuOpenFolder => "Ouvrir le dossier";

    // Msg
    public override string MsgTitleError => "Erreur";
    public override string MsgTitleInfo => "Information";
    public override string MsgTitleQuestion => "Confirmation";
    public override string MsgTitleWarning => "Avertissement";

    // Status
    public override string StatusAnalyzing => "Analyse en cours...";
    public override string StatusNoKey => "Aucune clé";

    // ToolTip
    public override string ToolTipAddFile => "Ajouter fichier";
    public override string ToolTipAddFolder => "Ajouter dossier";
    public override string ToolTipBlocklessMode => "Accès séquentiel (priorité espace)";
    public override string ToolTipBlockMode => "Accès aléatoire (priorité vitesse)";
    public override string ToolTipOpenWorkSpace => "Ouvrir le dossier de sortie";
    public override string ToolTipRemoveAll => "Vider";
    public override string ToolTipRemoveSelected => "Supprimer";
    public override string ToolTipValidateCompress => "Exécute une validation supplémentaire après la compression";
}