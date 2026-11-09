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
    public override string HintSelectOutput => "Sélectionnez le dossier de sortie";

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
    public override string HeaderCompressionRatio => "Taux de compression";
    public override string LabelOutputFormat => "Format de sortie :";

    // Log
    public override string LogAnalyzeMetadata => "━━ Analyse des métadonnées... ━━";
    public override string LogCompressAndMerge => "Compression et fusion en cours...";
    public override string LogCompressAndSplit => "Compression et division en cours...";
    public override string LogCompressionRatio => "Taux de compression";
    public override string LogDecompressAndMerge => "Décompression et fusion";
    public override string LogDeleteIncompleteFile => "Fichier incomplet supprimé.";
    public override string LogError(object arg0) => $"Une erreur est survenue : {arg0}";
    public override string LogFinalId(object arg0, object arg1) => $"ID final : {arg0} / Version : {arg1}";
    public override string LogMergeComplete(object arg0) => $"Fusion terminée : {arg0}";
    public override string LogMergeFailed(object? arg0, object arg1) => $"[{arg0}] Échec de la fusion : {arg1}";
    public override string LogMerging => "Fusion en cours...";
    public override string LogModeCompress => "Compression";
    public override string LogModeDecompress => "Décompression";
    public override string LogProcessError => "Erreur de traitement";
    public override string LogProcessStart => "Démarrage";
    public override string LogSplitComplete(object arg0) => $"Division terminée : {arg0}";
    public override string LogSplitFailed(object arg0, object arg1) => $"Échec de la division (ID : {arg0}) : {arg1}";
    public override string LogSplitPreparing(object arg0) => $"{arg0} Début de la division";
    public override string LogSplitting => "Division en cours...";
    public override string LogStatusCompressing => "Compression en cours...";
    public override string LogStatusCopying => "Copie en cours...";
    public override string LogStatusDecompressing => "Décompression...";
    public override string LogStatusDone => "Terminé";
    public override string LogStatusValidating => "Validation...";
    public override string LogTitleGroupDetected(object arg0) => $"{arg0} groupes de titres détectés";
    public override string LogValidationComplete => "Validation terminée";
    public override string LogValidationStart => "Démarrage de la validation";

    // Main
    public override string MainErrNoFiles => "Veuillez ajouter des fichiers et réessayer.";
    public override string MainErrNoKeys => "Fichier prod.keys introuvable. Veuillez placer le fichier de clés dans le dossier de l’application.";
    public override string MainErrNoOutput => "Veuillez définir le dossier de sortie.";
    public override string MainLogAllComplete(object arg0) => $"✓ Toutes les tâches terminées. Temps total : {arg0}";
    public override string MainLogRecalculating => "Réanalyse des types de fichiers...";
    public override string MainMsgDone => "Terminé !";
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
    public override string ToolTipAddFile => "Ajouter un fichier";
    public override string ToolTipAddFolder => "Ajouter un dossier";
    public override string ToolTipBlocklessMode => "Accès séquentiel (priorité espace)";
    public override string ToolTipBlockMode => "Accès aléatoire (priorité vitesse)";
    public override string ToolTipOpenWorkSpace => "Ouvrir le dossier de sortie";
    public override string ToolTipClear => "Vider";
    public override string ToolTipRemoveSelected => "Supprimer";
    public override string ToolTipValidateCompress => "Exécute une validation supplémentaire après la compression";
    public override string ButtonAddFile => "Ajouter un fichier";
    public override string ButtonAddFolder => "Ajouter un dossier";
    public override string ButtonRemoveSelected => "Supprimer";
    public override string ButtonClear => "Vider";
    public override string MenuRemoveSelected => "Supprimer";
    public override string MenuClear => "Vider";
    public override string TabPatch => "Patch";
    public override string LabelPatch => "Patch";
    public override string TabSettings => "Paramètres";
    public override string LabelSettings => "Paramètres";
    public override string LabelVersion => "Version";
    public override string HintDropCueChd => "Glissez-déposez un fichier CUE/CHD";
    public override string HintDropSourceNsp => "Glissez-déposez le fichier NSP/NSZ source";
    public override string HintDragOrSelectWorkFolder => "Faites glisser ou sélectionnez le dossier de travail";
    public override string HintDragOrSelectOutputFolder => "Faites glisser ou sélectionnez le dossier de sortie";
    public override string HintDropImageFile => "Glissez-déposez un fichier image";
    public override string HintDropFileOrFolder => "Glissez-déposez des fichiers ou des dossiers ici";
    public override string HintDropGameFileOrFolder => "Glissez-déposez des fichiers/dossiers de jeu ici";
    public override string HintRepackReplaceRule => "Le jeu de base et la mise à jour sont remplacés automatiquement, les DLC sont ajoutés";
    public override string HintSplitExpandRule => "Dépliez une ROM combinée avec le bouton + pour choisir le jeu de base, la mise à jour ou les DLC à diviser";
    public override string LabelGameName => "Nom du jeu";
    public override string LabelPublisher => "Éditeur";
    public override string LabelGameId => "ID du jeu";
    public override string LabelNspTitleId => "Title ID du NSP";
    public override string LabelWideScreen => "Écran large";
    public override string LabelSoundVolume => "Volume sonore :";
    public override string LabelLz4Compression => "Compression Lz4";
    public override string LabelSupportedLanguages => "Langues prises en charge";
    public override string LabelMultiRomIndex => "Index multi-ROM";
    public override string LabelMergeMode => "Mode de fusion :";
    public override string LabelEstimated => "Estimé";
    public override string LabelActual => "Réel";
    public override string LabelFileCount(object arg0) => $"{arg0} fichiers";
    public override string HeaderFileNameCompare => "Comparaison des noms de fichier (actuel/nouveau)";
    public override string DescConvertFormats => "Convertit entre les formats de fichier ci-dessous";
    public override string DescRenameByFormat => "Renomme les fichiers en lot selon le format sélectionné";
    public override string ButtonForceLanguage => "Forcer la langue";
    public override string ButtonCancelForceLanguage => "Annuler la langue forcée";
    public override string ButtonUnpack => "Extraire";
    public override string ButtonRepackFolderBased => "Repack (basé sur un dossier)";
    public override string ButtonUnpackAndRepackStreaming => "Extraire + Repack (basé sur le streaming)";
    public override string TabRepack => "Repack";
    public override string TabMerge => "Fusion";
    public override string TabSplit => "Division";
    public override string TabLz4Compression => "Compression Lz4";
    public override string TabFormatConvert => "Conversion de format";
    public override string TabSaturnNspConvert => "Conversion NSP Saturn";
    public override string TabIgnoreVersion => "Ignorer la version";
    public override string TabRename => "Renommer les fichiers";
    public override string RadioMergeGroupByTitleId => "Regrouper par Title ID (méthode actuelle)";
    public override string RadioMergeAll => "Tout";
    public override string MenuSelectFolder => "Sélectionner un dossier...";
    public override string MenuSelectArchive => "Sélectionner une archive (zip/7z)...";
    public override string MenuDeselect => "Désélectionner";
    public override string MenuFindInFolder => "Rechercher dans le dossier...";
    public override string MenuFindInArchive => "Rechercher dans l’archive (zip/7z)...";
    public override string ToolTipTitleIdInput => "Nombre hexadécimal à 16 chiffres (ex. : 0100000000010000). Laissez vide pour conserver le Title ID d’origine.";
    public override string ToolTipForceLanguage => "Lors du repack, seules les langues sélectionnées sont activées, toutes les autres sont désactivées.";
    public override string ToolTipMergeGroupByTitleId => "Regroupe les éléments par Title ID et fusionne chaque groupe dans un fichier distinct.";
    public override string ToolTipMergeAll => "Fusionne tous les éléments dans un seul fichier.";
    public override string ToolTipRomFsLz4 => "Compression Lz4 de la RomFS (appliquée lors du repack). Le jeu de base et la mise à jour sont fusionnés ; cocher l’un des deux active la compression.";
    public override string ToolTipBatchKoreanPatch => "Attribuer les patchs coréens en lot (dossier racine ou archive)";
    public override string ToolTipSelectAllToggle => "Tout sélectionner/désélectionner";
    public override string ToolTipExpandCollapse => "Développer/Réduire";
    public override string HeaderSize => "Taille";
    public override string ToolTipBrowseOutput => "Sélectionner le dossier de sortie";
}