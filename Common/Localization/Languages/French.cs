namespace Common.Localization.Languages;

public sealed class French : LocalizedStrings
{
    // Language
    public override string Code => "fr-FR";
    public override string DisplayName => "Français";
    public override string EnglishName => "French";

    // Button
    public override string ButtonAddFile => "Ajouter un fichier";
    public override string ButtonAddFolder => "Ajouter un dossier";
    public override string ButtonAnalyze => "Analyser";
    public override string ButtonCalculateHash => "Calculer le hash";
    public override string ButtonCancel => "Annuler";
    public override string ButtonCancelForceLanguage => "Annuler la langue forcée";
    public override string ButtonChange => "Modifier";
    public override string ButtonClear => "Vider";
    public override string ButtonDownload => "Télécharger";
    public override string ButtonFilterAll => "Tout";
    public override string ButtonFilterGame => "Jeu de base";
    public override string ButtonFilterUpdate => "Mise à jour";
    public override string ButtonForceLanguage => "Forcer la langue";
    public override string ButtonInstall => "Installer";
    public override string ButtonLater => "Plus tard";
    public override string ButtonLoadGameList => "Charger la liste des jeux";
    public override string ButtonOk => "OK";
    public override string ButtonRecoverTitleDb => "Récupérer title.db";
    public override string ButtonRemoveSelected => "Supprimer";
    public override string ButtonRepackFolderBased => "Repack (basé sur un dossier)";
    public override string ButtonReset => "Réinitialiser";
    public override string ButtonSelectFile => "Sélectionner un fichier";
    public override string ButtonSettings => "Paramètres";
    public override string ButtonStart => "Démarrer";
    public override string ButtonUnpack => "Extraire";
    public override string ButtonUnpackAndRepackFull => "Extraire + Repack (Full)";
    public override string ButtonUnpackAndRepackStreaming => "Extraire + Repack (basé sur le streaming)";

    // Check
    public override string CheckExtraOption => "Option supplémentaire (cmd 0x0B)";
    public override string CheckVideoFreezeFix => "Correctif du gel vidéo (cmd 0x00)";

    // Description
    public override string DescArchivePasswordProtected => "Cette archive est protégée par un mot de passe.";
    public override string DescAutoCompressByRule => "Le format est sélectionné automatiquement selon les règles ci-dessous, puis compressé ou décompressé";
    public override string DescAutoMatchByExtension => "L’association se fait automatiquement selon l’extension";
    public override string DescAutoVersionDateInName => "Lit la version et la date dans le nom du fichier de patch et les affiche dans le nom du fichier de sortie";
    public override string DescCalculateFileHash => "Calcule les hashs des fichiers";
    public override string DescCertsBinUsedForCia => "Le fichier certs.bin est utilisé lors de la création de fichiers CIA";
    public override string DescClickOrDropToChange => "Cliquez ou déposez un fichier pour modifier.";
    public override string DescConvertByRule => "La compression, la décompression et la conversion de format s’effectuent selon les règles ci-dessous";
    public override string DescConvertByRuleMutual => "Convertit de l’un à l’autre selon les règles ci-dessous";
    public override string DescConvertFormats => "Convertit entre les formats de fichier ci-dessous";
    public override string DescCreateSingleTrackCue => "Crée un fichier CUE à piste unique (multi-pistes non pris en charge)";
    public override string DescDecryptFiles => "Déchiffre les fichiers";
    public override string DescExtractCertsBin => "Extrait certs.bin à l’emplacement de l’exécutable";
    public override string DescInstallToConsole => "Installe des jeux sur une console 3DS";
    public override string DescMp3WavAutoConvertAt3 => "Les fichiers MP3 et WAV sont automatiquement convertis au format AT3 lors de leur ajout.";
    public override string DescOutputFolderDefault => "Si désactivé, enregistre dans le dossier output du dossier de la ROM source";
    public override string DescPackMax5 => "Jusqu’à 5 fichiers peuvent être empaquetés";
    public override string DescPc98Hdi => "Recherche et remplace les fichiers internes du HDI (FAT12/16) par chemin logique. Le chemin de montage est détecté automatiquement.";
    public override string DescRenameByFormat => "Renomme les fichiers en lot selon le format sélectionné";
    public override string DescRestorePbpToBinCue => "Restaure le PBP en BIN+CUE d’origine";
    public override string DescSelectSdLoadList => "Sélectionnez la carte SD et chargez la liste d’installation";
    public override string DescTistoryEnterUrl => "Saisissez une URL Tistory et cliquez sur Analyser";
    public override string DescTistoryFindAttachments => "Recherche les liens des pièces jointes et les ajoute à la liste";
    public override string DescVitaAutoClassify => "App/Patch/AddCont sont classés automatiquement";
    public override string DescVitaGroupToNoNpDrm => "Les App/Patch/AddCont ayant le même Title ID sont regroupés et convertis au format NoNpDrm";
    public override string DescVitaKeyRequiredShared => "Une clé (work.bin, *.rif/chaîne zRIF) est requise (Patch peut partager la clé de l’App)";
    public override string DescVitaPatchMerge => "Émulateur : écrase app/addcont après déchiffrement / Console : ajoute rePatch/reAddcont";
    public override string DescVitaPkgKeyRequired => "Pour un pkg, une clé (work.bin, *.rif/chaîne zRIF) est requise";
    public override string DescWiiUAutoClassify => "Le jeu de base, la mise à jour et les DLC sont classés automatiquement selon le Title ID";

    // Dialog
    public override string DialogSelectGameFile => "Sélectionner le fichier de jeu";

    // Error
    public override string ErrorNoMetadata => "Aucune métadonnée analysable disponible.";

    // Filter
    public override string FilterAllFiles => "Tous les fichiers";
    public override string FilterSwitchFiles => "Fichiers Switch";

    // Header
    public override string HeaderCompressionRatio => "Taux de compression";
    public override string HeaderConvert => "Conversion";
    public override string HeaderFileName => "Nom du fichier";
    public override string HeaderFileNameCompare => "Comparaison des noms de fichier (actuel/nouveau)";
    public override string HeaderFilePath => "Chemin";
    public override string HeaderForcedLanguage => "Langue forcée";
    public override string HeaderFormat => "Format";
    public override string HeaderInput => "Entrée";
    public override string HeaderKey => "Clé";
    public override string HeaderLocalTitle => "Titre local";
    public override string HeaderOutput => "Sortie";
    public override string HeaderPatch => "Patch";
    public override string HeaderProductCode => "Code produit";
    public override string HeaderResult => "Résultat";
    public override string HeaderSize => "Taille";
    public override string HeaderTitle => "Titre";
    public override string HeaderVersion => "Version";

    // Hint
    public override string HintDragOrClickPatch3ds => "Faites glisser ou cliquez pour choisir un dossier de patch ou un fichier zip/7z";
    public override string HintDragOrClickRiivolutionPatch => "Faites glisser ou cliquez pour choisir un dossier de patch Riivolution ou un zip/7z/xml";
    public override string HintDragOrClickRom3ds => "Faites glisser ou cliquez pour choisir un fichier ROM (.3ds, .cci, .zcci, .cia)";
    public override string HintDragOrClickWiiDisc => "Faites glisser ou cliquez pour choisir un disque Wii (.iso, .wbfs, .wia, .rvz)";
    public override string HintDragOrSelectOutputFolder => "Faites glisser ou sélectionnez le dossier de sortie";
    public override string HintDragOrSelectWorkFolder => "Faites glisser ou sélectionnez le dossier de travail";
    public override string HintDropArcadeFiles => "Glissez-déposez le ZIP source et les patchs (IPS, dossier, ZIP) ci-dessus";
    public override string HintDropCueChd => "Glissez-déposez un fichier CUE/CHD";
    public override string HintDropFile => "Glissez-déposez un fichier ici";
    public override string HintDropFileOrFolder => "Glissez-déposez des fichiers ou des dossiers ici";
    public override string HintDropGameFileOrFolder => "Glissez-déposez des fichiers/dossiers de jeu ici";
    public override string HintDropImageFile => "Glissez-déposez un fichier image";
    public override string HintDropOrClickAudio => "Glissez-déposez ou cliquez pour choisir un fichier audio";
    public override string HintDropPatchFile => "Glissez-déposez un fichier de patch";
    public override string HintDropSourceNsp => "Glissez-déposez le fichier NSP/NSZ source";
    public override string HintDropVitaFiles => "Glissez-déposez des fichiers (pkg, zip)/dossiers PS Vita ici";
    public override string HintDropVitaPkg => "Glissez-déposez des fichiers/dossiers PKG PS Vita ici";
    public override string HintDropWiiUFiles => "Glissez-déposez des dossiers WUP/Loadiine ou des fichiers wud/wux/wua ici";
    public override string HintRepackReplaceRule => "Le jeu de base et la mise à jour sont remplacés automatiquement, les DLC sont ajoutés";
    public override string HintSearchGame => "Rechercher des jeux...";
    public override string HintSelectOutput => "Sélectionnez le dossier de sortie";
    public override string HintSoundCancel => "Annulation (glissez-déposez un fichier .mp3 ou .wav)";
    public override string HintSoundComplete => "Terminé (glissez-déposez un fichier .mp3 ou .wav)";
    public override string HintSoundError => "Erreur (glissez-déposez un fichier .mp3 ou .wav)";
    public override string HintSplitExpandRule => "Dépliez une ROM combinée avec le bouton + pour choisir le jeu de base, la mise à jour ou les DLC à diviser";

    // Label
    public override string LabelActual => "Réel";
    public override string LabelAlgorithm => "Algorithme :";
    public override string LabelAllFormats => "Tous les formats";
    public override string LabelBackgroundMusic => "Musique de fond";
    public override string LabelBootLogo => "Logo de démarrage";
    public override string LabelCompression => "Compression";
    public override string LabelCompressionAlgorithmIso => "Algorithme de compression (ISO : PSP, PS2)";
    public override string LabelCompressionLevel => "Niveau de compression";
    public override string LabelCompressionLevelNszXcz => "Niveau de compression (NSZ, XCZ)";
    public override string LabelCompressionMode => "Mode de compression";
    public override string LabelDefaultImage => "Image par défaut";
    public override string LabelDownloadLocation => "Emplacement de téléchargement";
    public override string LabelEstimated => "Estimé";
    public override string LabelExpectedFileName => "Nom de fichier prévu :";
    public override string LabelFile => "Fichier";
    public override string LabelFileCount(object arg0) => $"{arg0} fichiers";
    public override string LabelFormat3dsCci => "3DS (même format que CCI)";
    public override string LabelFormatBinCueSingleTrack => "BIN+CUE (piste unique)";
    public override string LabelFormatChdIsoBased => "CHD (basé sur ISO)";
    public override string LabelFormatList3dsCciCia => "3DS (même format que CCI), CCI, CIA";
    public override string LabelFormatListIsoStandard => "ISO (standard), CUE+BIN, GDI+BIN+RAW, CCD+IMG+SUB";
    public override string LabelFormatListLoadiineFolderWua => "Dossier Loadiine, WUA";
    public override string LabelFormatListWuaWupLoadiine => "WUA, dossier WUP, dossier Loadiine";
    public override string LabelFormatListWupFolderWua => "Dossier WUP, WUA";
    public override string LabelFormatListWupLoadiine => "Dossier WUP, dossier Loadiine";
    public override string LabelGameId => "ID du jeu";
    public override string LabelGameName => "Nom du jeu";
    public override string LabelGeneral => "Général";
    public override string LabelLanguage => "Langue";
    public override string LabelLz4Compression => "Compression Lz4";
    public override string LabelMergeMode => "Mode de fusion :";
    public override string LabelMultiRomIndex => "Index multi-ROM";
    public override string LabelNoteCemuArchive => "(archive Cemu)";
    public override string LabelNoteDiscImage => "(image disque)";
    public override string LabelNoteFolder => "(dossier)";
    public override string LabelNspTitleId => "Title ID du NSP";
    public override string LabelOptions => "Options :";
    public override string LabelOthers => "Autres";
    public override string LabelOutputFormat => "Format de sortie :";
    public override string LabelOutputOptions => "Options de sortie";
    public override string LabelPatch => "Patch";
    public override string LabelPatchInfo => "Informations du patch";
    public override string LabelPbpPacking => "Empaquetage PBP";
    public override string LabelPkgCount(object arg0) => $"{arg0} fichiers PKG";
    public override string LabelPopsEmulatorSettings => "Paramètres de l’émulateur POPS";
    public override string LabelPublisher => "Éditeur";
    public override string LabelQuality => "Qualité";
    public override string LabelSelectPatch => "Sélectionner le patch :";
    public override string LabelSettings => "Paramètres";
    public override string LabelSoundVolume => "Volume sonore :";
    public override string LabelSource => "Source";
    public override string LabelSourceAutoExtractFormats => "Formats pris en charge pour l’extraction automatique de la source :";
    public override string LabelSupportedLanguages => "Langues prises en charge";
    public override string LabelSupportedPatchFormats => "Formats de patch pris en charge :";
    public override string LabelTistoryUrl => "URL Tistory";
    public override string LabelVersion => "Version";
    public override string LabelWideScreen => "Écran large";

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
    public override string MenuClear => "Vider";
    public override string MenuClearLog => "Effacer le journal";
    public override string MenuCopyLog => "Copier le journal";
    public override string MenuDeselect => "Désélectionner";
    public override string MenuExtractCci => "Extraire en CCI";
    public override string MenuExtractCia => "Extraire en CIA";
    public override string MenuFindInArchive => "Rechercher dans l’archive (zip/7z)...";
    public override string MenuFindInFolder => "Rechercher dans le dossier...";
    public override string MenuOpenFolder => "Ouvrir le dossier";
    public override string MenuRemoveSelected => "Supprimer";
    public override string MenuSelectArchive => "Sélectionner une archive (zip/7z)...";
    public override string MenuSelectFileIpsZip => "Sélectionner un fichier (*.ips, *.zip)...";
    public override string MenuSelectFileZip7z => "Sélectionner un fichier (*.zip, *.7z)...";
    public override string MenuSelectFileZip7zXml => "Sélectionner un fichier (*.zip, *.7z, *.xml)...";
    public override string MenuSelectFolder => "Sélectionner un dossier...";

    // Message
    public override string MsgTitleError => "Erreur";
    public override string MsgTitleInfo => "Information";
    public override string MsgTitleQuestion => "Confirmation";
    public override string MsgTitleWarning => "Avertissement";

    // Option
    public override string OptionLevel0None => "0 : Aucune compression";
    public override string OptionLevel18Recommended => "18 : Recommandé";
    public override string OptionLevel3Fastest => "3 : Vitesse maximale";
    public override string OptionLevel5Balanced => "5 : Équilibré";
    public override string OptionLevel9Balanced => "9 : Équilibré";
    public override string OptionLevel9Max => "9 : Maximum";

    // Radio
    public override string RadioBlock => "Bloc";
    public override string RadioBlockless => "Sans bloc";
    public override string RadioCciEmulator => "CCI (émulateur)";
    public override string RadioCiaConsole => "CIA (console)";
    public override string RadioConsoleRepatch => "Console (rePatch, reAddcont)";
    public override string RadioEmulatorAppAddcont => "Émulateur (app, addcont)";
    public override string RadioFolderPerTitleId => "Dossier (par Title ID)";
    public override string RadioLowercase => "Minuscules";
    public override string RadioMergeAll => "Tout";
    public override string RadioMergeGroupByTitleId => "Regrouper par Title ID (méthode actuelle)";
    public override string RadioOutputGameId => "Console (Title ID/EBOOT.PBP)";
    public override string RadioOutputNormal(object arg0) => $"Normal (nom du titre.{arg0})";
    public override string RadioQuality30s => "30 s (132k)";
    public override string RadioQuality60s => "60 s (66k)";
    public override string RadioRvzDolphin => "RVZ (compression exclusive à Dolphin)";
    public override string RadioUppercase => "Majuscules";
    public override string RadioWbfsUsbLoader => "WBFS (chargeur USB)";
    public override string RadioWuaEmulator => "WUA (émulateur)";
    public override string RadioWupConsole => "WUP (console)";
    public override string RadioZcciAzahar => "ZCCI (compression exclusive à Azahar)";
    public override string RadioZipPerTitleId => "ZIP (par Title ID)";

    // Status
    public override string StatusAnalyzing => "Analyse en cours...";
    public override string StatusConvertingToAt3 => "Conversion en AT3...";
    public override string StatusDownloadingImage => "Téléchargement de l’image...";
    public override string StatusHashCancelledByPatch => "Le calcul du hash a été annulé car le patch a démarré";
    public override string StatusHashFailed => "Échec du calcul du hash";
    public override string StatusLoading => "Chargement...";
    public override string StatusMatched => "Associé";
    public override string StatusNoKey => "Aucune clé";
    public override string StatusPresetLoaded => "Préréglage chargé (réglages manuels désactivés)";
    public override string StatusUnmatched => "Non associé";
    public override string StatusWrongPassword => "Le mot de passe est incorrect.";

    // Tab
    public override string TabArcade => "Arcade";
    public override string TabCalculateHash => "Calcul de hash";
    public override string TabConvert => "Conversion";
    public override string TabCreateCue => "Créer un Cue";
    public override string TabDecrypt => "Déchiffrement";
    public override string TabExtractCertsBin => "Extraire certs.bin";
    public override string TabFormatConvert => "Conversion de format";
    public override string TabGeneral => "Général";
    public override string TabIgnoreVersion => "Ignorer la version";
    public override string TabInstall => "Installation";
    public override string TabInstalledGames => "Jeux installés";
    public override string TabInstallNewGame => "Installer un nouveau jeu";
    public override string TabKeyFile => "Fichier de clés";
    public override string TabLz4Compression => "Compression Lz4";
    public override string TabMerge => "Fusion";
    public override string TabPatch => "Patch";
    public override string TabPbpPacking => "Empaquetage PBP";
    public override string TabPbpUnpack => "Extraction PBP";
    public override string TabRename => "Renommer les fichiers";
    public override string TabRepack => "Repack";
    public override string TabSaturnNspConvert => "Conversion NSP Saturn";
    public override string TabSettings => "Paramètres";
    public override string TabSplit => "Division";
    public override string TabTistoryDownloader => "Téléchargeur Tistory";
    public override string TabUtil => "Utilitaires";

    // Title
    public override string TitleArchivePassword => "Mot de passe de l’archive";

    // Toggle
    public override string ToggleAlwaysOnTop => "Toujours afficher au-dessus des autres fenêtres";
    public override string ToggleAutoCompressAfterPatch => "Compresser automatiquement après le patch";
    public override string ToggleAutoExtractDelete => "Extraire automatiquement les archives (supprimer l’original ensuite)";
    public override string ToggleAutoVersionDateInName => "Ajouter automatiquement la version/date au nom du fichier de sortie";
    public override string ToggleChangeOutputFolder => "Modifier le dossier de sortie";
    public override string ToggleCheckUpdateOnStart => "Rechercher les mises à jour au démarrage";
    public override string ToggleMergePatchToGame => "Fusionner le patch dans le jeu";
    public override string ToggleOtherFormats => "Autres";
    public override string ToggleSoundEffects => "Effets sonores";

    // ToolTip
    public override string ToolTipAddFile => "Ajouter un fichier";
    public override string ToolTipAddFolder => "Ajouter un dossier";
    public override string ToolTipAddSequentialPatch => "Ajouter un patch à appliquer en séquence";
    public override string ToolTipAutoExtractDelete => "Si un fichier téléchargé est une archive (zip/7z/rar), elle est extraite automatiquement et l’archive d’origine est supprimée";
    public override string ToolTipBatchKoreanPatch => "Attribuer les patchs coréens en lot (dossier racine ou archive)";
    public override string ToolTipBlocklessMode => "Accès séquentiel (priorité espace)";
    public override string ToolTipBlockMode => "Accès aléatoire (priorité vitesse)";
    public override string ToolTipBrowseOutput => "Sélectionner le dossier de sortie";
    public override string ToolTipChangePatchFile => "Passer à un autre fichier de patch";
    public override string ToolTipChangePatchPackage => "Passer à un autre paquet de patchs";
    public override string ToolTipClear => "Vider";
    public override string ToolTipCopyHash => "Copier le hash";
    public override string ToolTipDragFolderToChangeDownload => "Vous pouvez glisser un dossier ici pour modifier l’emplacement de téléchargement.";
    public override string ToolTipExpandCollapse => "Développer/Réduire";
    public override string ToolTipForcedLanguage => "Fixe les paramètres régionaux sur cette langue lors de l’installation (locale.txt généré automatiquement). Seules les langues réellement présentes dans le SMDH sont affichées.";
    public override string ToolTipForceLanguage => "Lors du repack, seules les langues sélectionnées sont activées, toutes les autres sont désactivées.";
    public override string ToolTipGameNameMax63 => "Nom du jeu (63 caractères max.)";
    public override string ToolTipLicenseKey => "Chemin du fichier de licence (work.bin, *.rif)/chaîne zRIF";
    public override string ToolTipLicenseKeyPatchFallback => "Chemin du fichier de licence (work.bin, *.rif)/chaîne zRIF (Patch utilise la clé de l’App si le champ est vide)";
    public override string ToolTipMergeAll => "Fusionne tous les éléments dans un seul fichier.";
    public override string ToolTipMergeGroupByTitleId => "Regroupe les éléments par Title ID et fusionne chaque groupe dans un fichier distinct.";
    public override string ToolTipMovableSedGuide => "1. Lancez GodMode9 (START + POWER)\n2. Sélectionnez SYSNAND CTRNAND → private → movable.sed\n3. Exécutez Copy to 0:/gm9/out tout en bas";
    public override string ToolTipOpenGithubReleases => "Ouvre la page des versions GitHub";
    public override string ToolTipOpenHelp => "Ouvre la page d’aide";
    public override string ToolTipOpenWorkSpace => "Ouvrir le dossier de sortie";
    public override string ToolTipPlayPause => "Lecture/Pause";
    public override string ToolTipPublisherMax63 => "Éditeur (63 caractères max.)";
    public override string ToolTipRecoverTitleDb => "À utiliser lorsqu’un jeu installé sur une console 3DS n’apparaît pas dans le menu HOME ou n’est pas reconnu à cause d’une erreur de title.db.\n\nRecrée title.db et import.db à partir des titres installés sur la carte SD.\nLes title.db/import.db existants sont supprimés ; les fichiers de jeu ne sont pas modifiés.\nSi tout est reconnu normalement, il n’est pas nécessaire de cliquer.";
    public override string ToolTipRemoveSelected => "Supprimer";
    public override string ToolTipRestoreDefaultPath => "Restaurer l’emplacement par défaut";
    public override string ToolTipRomFsLz4 => "Compression Lz4 de la RomFS (appliquée lors du repack). Le jeu de base et la mise à jour sont fusionnés ; cocher l’un des deux active la compression.";
    public override string ToolTipSelectAllToggle => "Tout sélectionner/désélectionner";
    public override string ToolTipSelectFile => "Sélectionner un fichier";
    public override string ToolTipTistoryUrlInput => "Saisissez une URL d’article Tistory par ligne (plusieurs possibles, Ctrl+Entrée pour analyser)";
    public override string ToolTipTitleIdInput => "Nombre hexadécimal à 16 chiffres (ex. : 0100000000010000). Laissez vide pour conserver le Title ID d’origine.";
    public override string ToolTipValidateCompress => "Exécute une validation supplémentaire après la compression";

    // Update
    public override string LatestVersion => "Dernière version";
    public override string NoUpdateNotes => "Aucune note de mise à jour disponible.";
    public override string UpdateAvailable => "Mise à jour disponible";
    public override string UpdateDescription => "Une nouvelle version de RomForge a été publiée.";
    public override string UpdateTitle => "Mise à jour de RomForge";

    public override string StatusWaiting => "En attente";
    public override string StatusConverting => "Conversion";
    public override string StatusDecrypting => "Déchiffrement";
    public override string StatusExtracting => "Extraction";
    public override string StatusDownloading => "Téléchargement";
    public override string StatusUnzipping => "Décompression";
    public override string StatusUnzipDone => "Décompressé";
    public override string StatusUnzipFailed => "Échec de la décompression";
    public override string StatusDone => "Terminé";
    public override string StatusFailed => "Échec";
    public override string StatusCancelled => "Annulé";
    public override string StatusUnsupported => "Non pris en charge";
    public override string StatusSkipped => "Ignoré";
    public override string RenameStatusReady => "À renommer";
    public override string RenameStatusUnchanged => "Inchangé";
    public override string RenameStatusMissingInfo => "Aucune info";
    public override string RenameStatusEmptyName => "Sans nom";
    public override string RenameStatusNameTooLong => "Nom trop long";
    public override string RenameStatusDuplicate => "Nom en double";
    public override string RenameStatusTargetExists => "Fichier existant";

    public override string ProgressElapsed(object arg0) => $"{arg0} écoulé";
    public override string ProgressCompletedCount(object arg0, object arg1) => $"{arg0} / {arg1} terminé";
    public override string ProgressHashingPercent(object arg0) => $"Calcul du hash... {arg0}%";
    public override string ProgressAnalyzingPatchInfo => "Analyse des infos du patch...";
    public override string ProgressExtractingPatch => "Décompression du patch...";
    public override string ProgressUnpackingFile(object arg0) => $"Extraction : {arg0}";
    public override string ProgressRepackingFile(object arg0) => $"Repack en cours : {arg0}";
    public override string ProgressApplyingPatchTo(object arg0) => $"Application du patch : {arg0}";
    public override string ProgressHashingPartitions(object arg0) => $"Calcul des hash des partitions : {arg0}";
    public override string ProgressCompressingWbfs(object arg0) => $"Compression WBFS : {arg0}";
    public override string ProgressCompressingRvz(object arg0) => $"Compression RVZ : {arg0}";
    public override string ProgressCreatingIso(object arg0) => $"Création de l'ISO : {arg0}";
    public override string ProgressWupCreated(object arg0, object arg1) => $"WUP créé : {arg0}_v{arg1}";
    public override string MsgCancelled => "Annulé.";
    public override string StatusInitializing => "Initialisation...";
    public override string StatusSelectSdAndMovable => "Sélectionnez la carte SD et movable.sed";
    public override string ProgressLoadingCount(object arg0, object arg1) => $"Chargement... {arg0} / {arg1}";
    public override string ProgressScanningCount(object arg0, object arg1) => $"Analyse... {arg0} / {arg1}";
    public override string StatusRecoveringTitleDb => "Récupération de title.db...";
    public override string StatusRecoveryDone(object arg0) => $"Récupération terminée : {arg0} titres";
    public override string StatusExtractingTitle(object? arg0, object arg1) => $"Extraction : {arg0} - {arg1}";
    public override string StatusInstallStarted(object? arg0) => $"Installation démarrée : {arg0}";
    public override string StatusInstallDone(object? arg0) => $"Installation terminée : {arg0}";
    public override string StatusScanningTitles => "Analyse des titres...";
    public override string StatusLoadingMetadata(object arg0) => $"Chargement des métadonnées... ({arg0} au total)";
    public override string StatusTitlesLoaded(object arg0) => $"{arg0} titres chargés";
    public override string ProgressPreparing => "Préparation";
    public override string ProgressHashing => "Calcul du hash";
    public override string ProgressCompressingNsz => "Compression NSZ";
}