namespace Common.Localization;

public abstract class LocalizedStrings
{
    // Language
    public abstract string Code { get; }
    public abstract string DisplayName { get; }
    public abstract string EnglishName { get; }

    // Action
    public abstract string ActionLoad { get; }

    // Button
    public abstract string ButtonAddFile { get; }
    public abstract string ButtonAddFolder { get; }
    public abstract string ButtonAnalyze { get; }
    public abstract string ButtonCalculateHash { get; }
    public abstract string ButtonCancel { get; }
    public abstract string ButtonCancelForceLanguage { get; }
    public abstract string ButtonChange { get; }
    public abstract string ButtonClear { get; }
    public abstract string ButtonCueBasis { get; }
    public abstract string ButtonDownload { get; }
    public abstract string ButtonFilterAll { get; }
    public abstract string ButtonFilterGame { get; }
    public abstract string ButtonFilterUpdate { get; }
    public abstract string ButtonForceLanguage { get; }
    public abstract string ButtonGdiBasis { get; }
    public abstract string ButtonInstall { get; }
    public abstract string ButtonLater { get; }
    public abstract string ButtonLoadGameList { get; }
    public abstract string ButtonOk { get; }
    public abstract string ButtonRecoverTitleDb { get; }
    public abstract string ButtonRemoveSelected { get; }
    public abstract string ButtonRepackFolderBased { get; }
    public abstract string ButtonReset { get; }
    public abstract string ButtonSelect { get; }
    public abstract string ButtonSelectFile { get; }
    public abstract string ButtonSettings { get; }
    public abstract string ButtonStart { get; }
    public abstract string ButtonUnpack { get; }
    public abstract string ButtonUnpackAndRepackFull { get; }
    public abstract string ButtonUnpackAndRepackStreaming { get; }

    // Check
    public abstract string CheckExtraOption { get; }
    public abstract string CheckVideoFreezeFix { get; }

    // Description
    public abstract string DescArchivePasswordProtected { get; }
    public abstract string DescAutoCompressByRule { get; }
    public abstract string DescAutoMatchByExtension { get; }
    public abstract string DescAutoVersionDateInName { get; }
    public abstract string DescCalculateFileHash { get; }
    public abstract string DescCertsBinUsedForCia { get; }
    public abstract string DescClickOrDropToChange { get; }
    public abstract string DescConvertByRule { get; }
    public abstract string DescConvertByRuleMutual { get; }
    public abstract string DescConvertFormats { get; }
    public abstract string DescCreateSingleTrackCue { get; }
    public abstract string DescDecryptFiles { get; }
    public abstract string DescExtractCertsBin { get; }
    public abstract string DescInstallToConsole { get; }
    public abstract string DescMp3WavAutoConvertAt3 { get; }
    public abstract string DescOutputFolderDefault { get; }
    public abstract string DescPackMax5 { get; }
    public abstract string DescPc98Hdi { get; }
    public abstract string DescRenameByFormat { get; }
    public abstract string DescRestorePbpToBinCue { get; }
    public abstract string DescSelectSdLoadList { get; }
    public abstract string DescTistoryEnterUrl { get; }
    public abstract string DescTistoryFindAttachments { get; }
    public abstract string DescVitaAutoClassify { get; }
    public abstract string DescVitaGroupToNoNpDrm { get; }
    public abstract string DescVitaKeyRequiredShared { get; }
    public abstract string DescVitaPatchMerge { get; }
    public abstract string DescVitaPkgKeyRequired { get; }
    public abstract string DescWiiUAutoClassify { get; }

    // Detail
    public abstract string DetailAssignFailedNothingFound { get; }
    public abstract string DetailAssignWarnUnrelatedFolders(object? arg0, object? arg1);
    public abstract string DetailNoMatchingFolder(object? arg0, object? arg1);

    // Dialog
    public abstract string DialogSelect3dsRom { get; }
    public abstract string DialogSelectCiaFile { get; }
    public abstract string DialogSelectDownloadFolder { get; }
    public abstract string DialogSelectFile { get; }
    public abstract string DialogSelectFileToConvert { get; }
    public abstract string DialogSelectFileToDecrypt { get; }
    public abstract string DialogSelectFileToInstall { get; }
    public abstract string DialogSelectFileToRename { get; }
    public abstract string DialogSelectFolderToAdd { get; }
    public abstract string DialogSelectFolderToAddWupOrLoadiine { get; }
    public abstract string DialogSelectGameFile { get; }
    public abstract string DialogSelectGameFolder { get; }
    public abstract string DialogSelectHashBasis { get; }
    public abstract string DialogSelectKeyFile(object? arg0);
    public abstract string DialogSelectKoreanPatch { get; }
    public abstract string DialogSelectKoreanPatchArchive { get; }
    public abstract string DialogSelectKoreanPatchArchiveFor(object? arg0);
    public abstract string DialogSelectKoreanPatchFolder { get; }
    public abstract string DialogSelectKoreanPatchFolderFor(object? arg0);
    public abstract string DialogSelectKoreanPatchRootArchive { get; }
    public abstract string DialogSelectKoreanPatchRootFolder { get; }
    public abstract string DialogSelectMovableSed { get; }
    public abstract string DialogSelectPatchFile { get; }
    public abstract string DialogSelectPatchFolder { get; }
    public abstract string DialogSelectRiivolutionPatch { get; }
    public abstract string DialogSelectRiivolutionPatchFolder { get; }
    public abstract string DialogSelectSaveLocation(object? arg0);
    public abstract string DialogSelectSourceFile { get; }
    public abstract string DialogSelectSourceHdi { get; }
    public abstract string DialogSelectSourceRomFile { get; }
    public abstract string DialogSelectSourceWiiDisc { get; }
    public abstract string DialogSelectSourceZip { get; }
    public abstract string DialogSelectWiiDiscFile { get; }
    public abstract string DialogSelectWorkFolder { get; }

    // Error
    public abstract string Err7zLoadPathUnknown { get; }
    public abstract string Err7zModuleHandleNotFound { get; }
    public abstract string ErrAbsoluteLbaDataTrackNotFound(object? arg0);
    public abstract string ErrAbsoluteLbaSectorEof(object? arg0);
    public abstract string ErrAddAtLeastOneTitle { get; }
    public abstract string ErrApploaderOverlapsDol { get; }
    public abstract string ErrApploaderSizeInvalid { get; }
    public abstract string ErrArchiveCandidatesNoSelector { get; }
    public abstract string ErrArchiveCcdImgMissing { get; }
    public abstract string ErrArchiveCueBinMissing { get; }
    public abstract string ErrArchiveEmpty { get; }
    public abstract string ErrArchiveEntryRefindFailed(object? arg0);
    public abstract string ErrArchiveGdiTrackMissing { get; }
    public abstract string ErrArchiveNoHashTarget { get; }
    public abstract string ErrArchiveNoPatchTarget { get; }
    public abstract string ErrArchivePasswordRequired(object? arg0);
    public abstract string ErrArchivePathNotFound(object? arg0);
    public abstract string ErrArchiveSelectedFileMissing { get; }
    public abstract string ErrAt3FrameSizeInvalid { get; }
    public abstract string ErrAt3NoAudioData { get; }
    public abstract string ErrAt3StructureInvalid { get; }
    public abstract string ErrBaseNotInList { get; }
    public abstract string ErrBaseProgramNcaNotFound { get; }
    public abstract string ErrBaseUpdateTitleMismatch { get; }
    public abstract string ErrBi2BinSize { get; }
    public abstract string ErrBinFileCreateFailed(object? arg0);
    public abstract string ErrBinFileMissing(object? arg0);
    public abstract string ErrBinFileNotFoundName(object? arg0);
    public abstract string ErrBitrateMustBeOneOf { get; }
    public abstract string ErrBitstreamTooShort { get; }
    public abstract string ErrBlockDecompressFailed(object? arg0);
    public abstract string ErrBlockLz4DecodeFailed(object? arg0, object? arg1, object? arg2, object? arg3, object? arg4, object? arg5, object? arg6, object? arg7);
    public abstract string ErrBlockLz4DecodeFailedShort(object? arg0, object? arg1, object? arg2, object? arg3, object? arg4, object? arg5);
    public abstract string ErrBootBinSize { get; }
    public abstract string ErrBpsCommandLengthOutOfRange { get; }
    public abstract string ErrBpsMetadataSizeInvalid { get; }
    public abstract string ErrBpsResultSizeMismatch { get; }
    public abstract string ErrBpsSourceCopyOutOfRange { get; }
    public abstract string ErrBpsSourceReadOutOfRange { get; }
    public abstract string ErrBpsTargetCopyInvalid { get; }
    public abstract string ErrBpsTargetReadOutOfRange { get; }
    public abstract string ErrBzip2RvzUnsupported { get; }
    public abstract string ErrCannotRestoreCci(object? arg0);
    public abstract string ErrCcdEntryTrackSectorRangeInvalid(object? arg0, object? arg1, object? arg2, object? arg3, object? arg4);
    public abstract string ErrCcdNoValidTrackSection(object? arg0);
    public abstract string ErrCcdPairedFileNotFound(object? arg0, object? arg1);
    public abstract string ErrCcdTrackMissingModeIndex { get; }
    public abstract string ErrCcdTrackSectorRangeInvalid(object? arg0, object? arg1, object? arg2, object? arg3, object? arg4);
    public abstract string ErrCertsBinNeeded { get; }
    public abstract string ErrChdConvertFailed(object? arg0);
    public abstract string ErrChdCreateFailed { get; }
    public abstract string ErrChdDecompressFailed(object? arg0);
    public abstract string ErrChdExtractFailed { get; }
    public abstract string ErrChdFileNotFound { get; }
    public abstract string ErrChdHeaderUnreadable { get; }
    public abstract string ErrChdInfoParseFailed { get; }
    public abstract string ErrChdNotConvertibleToCso(object? arg0, object? arg1, object? arg2);
    public abstract string ErrChdNotExtractableToIso(object? arg0, object? arg1, object? arg2);
    public abstract string ErrChdOpenFailed(object? arg0);
    public abstract string ErrChdParentRequiredNoHash { get; }
    public abstract string ErrChdTrackInfoUnreadable { get; }
    public abstract string ErrChdmanInfoFailed(object? arg0);
    public abstract string ErrCiaNeedsKeys { get; }
    public abstract string ErrCiaNeedsSeekableOutput { get; }
    public abstract string ErrCifinishMagicMismatch { get; }
    public abstract string ErrCifinishVersionMismatch(object? arg0);
    public abstract string ErrCnmtContentRecordLimit { get; }
    public abstract string ErrCnmtContentRecordNotFound { get; }
    public abstract string ErrCnmtRecordAreaOutOfRange { get; }
    public abstract string ErrCnmtSizeInvalid { get; }
    public abstract string ErrCommonKeyMissing(object? arg0);
    public abstract string ErrCompressedTitleBlockEmpty { get; }
    public abstract string ErrCompressorAcquireFailed { get; }
    public abstract string ErrContent0NotFst { get; }
    public abstract string ErrContentBinMissing(object? arg0);
    public abstract string ErrContentFileNotFound(object? arg0);
    public abstract string ErrContentFilePathMissing(object? arg0);
    public abstract string ErrContentIndexNotFound(object? arg0);
    public abstract string ErrContentReadOutOfRange(object? arg0);
    public abstract string ErrContentSdPathMissing(object? arg0);
    public abstract string ErrControlNacpFileMissing { get; }
    public abstract string ErrControlNcaFileMissing { get; }
    public abstract string ErrControlNcaNotFound { get; }
    public abstract string ErrCsoMagicMismatch { get; }
    public abstract string ErrCueFileCreateFailed { get; }
    public abstract string ErrCueFileEntryNotFoundIn(object? arg0);
    public abstract string ErrCueFileNoTrack(object? arg0, object? arg1);
    public abstract string ErrCueFileNotFound(object? arg0);
    public abstract string ErrCueNoBinReference(object? arg0);
    public abstract string ErrCueNoFileEntry(object? arg0);
    public abstract string ErrCueNoValidTrack(object? arg0);
    public abstract string ErrCueNotFoundInRomfs { get; }
    public abstract string ErrCueRefBinMissing { get; }
    public abstract string ErrCueReferencedBinFileNotFound(object? arg0);
    public abstract string ErrCueReferencedBinNotFound { get; }
    public abstract string ErrCueTrackRangeInvalid(object? arg0, object? arg1, object? arg2, object? arg3, object? arg4);
    public abstract string ErrDcpGdiNotFound { get; }
    public abstract string ErrDcpGdiSourceOnly { get; }
    public abstract string ErrDcpNoChain { get; }
    public abstract string ErrDcpSourceMissing(object? arg0);
    public abstract string ErrDecompressedLargerThanExpected { get; }
    public abstract string ErrDestBufferTooSmall { get; }
    public abstract string ErrDirIndexNotFound(object? arg0);
    public abstract string ErrDirStructureCyclic { get; }
    public abstract string ErrDirectoryNotReadableByReadFile(object? arg0);
    public abstract string ErrDiscHeaderUnreadable { get; }
    public abstract string ErrDiscImageTooSmall { get; }
    public abstract string ErrDiscReadOutOfRange { get; }
    public abstract string ErrDiscUnreadable { get; }
    public abstract string ErrDlcNoCciConversion { get; }
    public abstract string ErrDlcNoCiaConversion { get; }
    public abstract string ErrDolSizeInvalid { get; }
    public abstract string ErrDuplicatePathCaseSensitive(object? arg0);
    public abstract string ErrEnterGameName { get; }
    public abstract string ErrErrnoEacces { get; }
    public abstract string ErrErrnoEnoent { get; }
    public abstract string ErrErrnoEnospc { get; }
    public abstract string ErrExeFsHeaderCorrupt { get; }
    public abstract string ErrExeFsNoFiles { get; }
    public abstract string ErrExeFsTooManyEntries(object? arg0);
    public abstract string ErrFat16VolumeNotFound { get; }
    public abstract string ErrFileDataNotRegistered(object? arg0);
    public abstract string ErrFileNameTableExceeded { get; }
    public abstract string ErrFileNameTooLong(object? arg0);
    public abstract string ErrFileNotFoundInWup(object? arg0);
    public abstract string ErrFileNotFoundName(object? arg0);
    public abstract string ErrFileSizeDiffersFromHeader(object? arg0);
    public abstract string ErrFileTooLargeUnder4Gb(object? arg0);
    public abstract string ErrFilesDbBadFileCount(object? arg0);
    public abstract string ErrFilesDbMagicInvalid { get; }
    public abstract string ErrFilesDbNotFound { get; }
    public abstract string ErrFilesFolderEmpty { get; }
    public abstract string ErrFmtChunkInvalid { get; }
    public abstract string ErrFrameSizeInvalid { get; }
    public abstract string ErrFstClusterCountAbnormal { get; }
    public abstract string ErrFstDirNestingTooDeep { get; }
    public abstract string ErrFstDirRangeInvalid { get; }
    public abstract string ErrFstDirStructureInvalid { get; }
    public abstract string ErrFstEntryCountInvalid { get; }
    public abstract string ErrFstNameAreaTooLarge { get; }
    public abstract string ErrFstNameOffsetInvalid { get; }
    public abstract string ErrFstPositionOrSizeInvalid { get; }
    public abstract string ErrFstSizeInvalid { get; }
    public abstract string ErrGamePartitionNotFound { get; }
    public abstract string ErrGczBlockCountShort { get; }
    public abstract string ErrGczBlockDecompressedSize(object? arg0);
    public abstract string ErrGczBlockHashMismatch(object? arg0);
    public abstract string ErrGczBlockPointerInvalid { get; }
    public abstract string ErrGczBlockUncompressedSize(object? arg0);
    public abstract string ErrGczCompressedBlockTooLarge { get; }
    public abstract string ErrGczDataLargerThanFile { get; }
    public abstract string ErrGczHeaderLargerThanFile { get; }
    public abstract string ErrGczReadOutOfRange { get; }
    public abstract string ErrGczUncompressedBlockSizeInvalid { get; }
    public abstract string ErrGdiFormatInvalid { get; }
    public abstract string ErrGdiNotFound(object? arg0);
    public abstract string ErrGdiTrackCountMismatch(object? arg0, object? arg1);
    public abstract string ErrGdiTrackCountUnreadable { get; }
    public abstract string ErrGdiTrackLineInvalid(object? arg0);
    public abstract string ErrHashTreeBlockHeaderUnreadable { get; }
    public abstract string ErrHdiNoMatchingFiles { get; }
    public abstract string ErrHeaderBinMissing(object? arg0);
    public abstract string ErrHeaderInvalid(object? arg0);
    public abstract string ErrHighDensityDataTrackNotFound { get; }
    public abstract string ErrHuffmanCodeInvalid { get; }
    public abstract string ErrIconWriteFailed(object? arg0);
    public abstract string ErrId0DirNotFound(object? arg0);
    public abstract string ErrId1DirNotFound { get; }
    public abstract string ErrImgSizeNotSectorMultiple(object? arg0);
    public abstract string ErrIndexShiftBlockOffsetOverflow(object? arg0, object? arg1);
    public abstract string ErrIndexShiftFinalOffsetOverflow(object? arg0);
    public abstract string ErrInputCrc32Mismatch { get; }
    public abstract string ErrInputDataEmpty { get; }
    public abstract string ErrInvalidKeyGeneration(object? arg0);
    public abstract string ErrInvalidNcsdStructure { get; }
    public abstract string ErrInvalidNsoFile { get; }
    public abstract string ErrInvalidOutputFormat { get; }
    public abstract string ErrInvalidPatchHeader(object? arg0);
    public abstract string ErrInvalidTitleId { get; }
    public abstract string ErrIsoExtractFailed { get; }
    public abstract string ErrIsoPvdNotFound { get; }
    public abstract string ErrIsoSingleDataTrackOnly(object? arg0);
    public abstract string ErrIsoUnsupportedTrackType(object? arg0);
    public abstract string ErrIvfcHashNotStarted { get; }
    public abstract string ErrIvfcInputEndedEarly { get; }
    public abstract string ErrIvfcLevelSizeMismatch(object? arg0, object? arg1, object? arg2);
    public abstract string ErrIvfcMagicMismatch { get; }
    public abstract string ErrIvfcTypeIdInvalid { get; }
    public abstract string ErrJointStereoUnitHeaderInvalid { get; }
    public abstract string ErrKeyAreaKeyNotSet { get; }
    public abstract string ErrKeyFileInvalid { get; }
    public abstract string ErrLabelFileCreateFailed(object? arg0);
    public abstract string ErrLanguageIndexOutOfRange(object? arg0, object? arg1);
    public abstract string ErrLoadMovableFirst { get; }
    public abstract string ErrLz77CodeTooSmall { get; }
    public abstract string ErrLz77FooterInvalid { get; }
    public abstract string ErrLz77OutOfRange { get; }
    public abstract string ErrLz77VerifyFailed { get; }
    public abstract string ErrLzma2RvzUnsupported { get; }
    public abstract string ErrLzmaDataInvalid { get; }
    public abstract string ErrLzmaDataTooShort { get; }
    public abstract string ErrLzmaDataTruncated { get; }
    public abstract string ErrLzmaDistanceInvalid { get; }
    public abstract string ErrLzmaPropsInvalid { get; }
    public abstract string ErrLzmaPropsSize { get; }
    public abstract string ErrLzmaStreamHeaderInvalid { get; }
    public abstract string ErrMainDolSizeInvalid { get; }
    public abstract string ErrMainNpdmNotFound { get; }
    public abstract string ErrMdsFileTooSmall(object? arg0);
    public abstract string ErrMdsIndexBlockOffsetInvalid(object? arg0, object? arg1);
    public abstract string ErrMdsNoValidTrack(object? arg0);
    public abstract string ErrMdsPairedMdfNotFound(object? arg0);
    public abstract string ErrMdsSessionBlockOffsetInvalid(object? arg0, object? arg1);
    public abstract string ErrMdsSessionZeroBlocks(object? arg0);
    public abstract string ErrMdsSignatureMismatch(object? arg0);
    public abstract string ErrMdsTrackBlockAreaOutOfFile(object? arg0, object? arg1);
    public abstract string ErrMdsTrackBlockOffsetInvalid(object? arg0, object? arg1);
    public abstract string ErrMdsTrackDataRangeOutOfMdf(object? arg0, object? arg1, object? arg2, object? arg3, object? arg4);
    public abstract string ErrMetaNcaCnmtNotFound { get; }
    public abstract string ErrMetaNcaNotFound(object? arg0);
    public abstract string ErrMetaPartitionHeaderOffsetInvalid { get; }
    public abstract string ErrMetaSizeInvalid { get; }
    public abstract string ErrMetaTmdCertSizeMismatch { get; }
    public abstract string ErrMetadataTooLarge(object? arg0);
    public abstract string ErrMonoOrStereoOnly { get; }
    public abstract string ErrMovableMagicMismatch { get; }
    public abstract string ErrMovableTooShort { get; }
    public abstract string ErrMultipleId1Dirs(object? arg0);
    public abstract string ErrMultipleXmlSpecifyOne(object? arg0, object? arg1);
    public abstract string ErrNative7zLoadFailed { get; }
    public abstract string ErrNcaHashMustBe32Bytes { get; }
    public abstract string ErrNcaIdHashSize { get; }
    public abstract string ErrNcaSizeMismatch(object? arg0, object? arg1);
    public abstract string ErrNcchHeaderTooShort { get; }
    public abstract string ErrNcchMagicMismatch { get; }
    public abstract string ErrNcchNoExeFs { get; }
    public abstract string ErrNcchNoRomFs { get; }
    public abstract string ErrNcsdMagicMismatch { get; }
    public abstract string ErrNoApplicationOrDlcMeta { get; }
    public abstract string ErrNoDataToConvert { get; }
    public abstract string ErrNoExtractableContent { get; }
    public abstract string ErrNoFilesToReplace { get; }
    public abstract string ErrNoFreeClusters { get; }
    public abstract string ErrNoKeys { get; }
    public abstract string ErrNoRomfsForLz4 { get; }
    public abstract string ErrNoTitleForIdOffset(object? arg0);
    public abstract string ErrNoTitlesToPack { get; }
    public abstract string ErrNoTmdRead { get; }
    public abstract string ErrNoUnpackedData { get; }
    public abstract string ErrNoUnpackedPartitions { get; }
    public abstract string ErrNoValidNcchPartition { get; }
    public abstract string ErrNoValidTitleInfoAfterNacpUpdate { get; }
    public abstract string ErrNotFormatFile(object? arg0);
    public abstract string ErrNotGameCubeDisc { get; }
    public abstract string ErrNotGcOrWiiDisc { get; }
    public abstract string ErrNotRiivolutionXml { get; }
    public abstract string ErrNotWiiDisc { get; }
    public abstract string ErrNsoSegmentLz4Failed(object? arg0, object? arg1);
    public abstract string ErrNspHasNoFiles { get; }
    public abstract string ErrOnlyBinaryFileType(object? arg0, object? arg1);
    public abstract string ErrOnlyCueChdSupported { get; }
    public abstract string ErrOnlyFolderIpsZipRegistrable { get; }
    public abstract string ErrOnlyNspNszXciXczSupported { get; }
    public abstract string ErrOnlyZipSelectable { get; }
    public abstract string ErrOpenDownloadPageFailed(object? arg0);
    public abstract string ErrOutputBufferTooSmall { get; }
    public abstract string ErrOutputCrc32Mismatch { get; }
    public abstract string ErrOutputStreamNeedsRwSeek { get; }
    public abstract string ErrPackageAlreadyFinalized { get; }
    public abstract string ErrPackageFileLimit(object? arg0);
    public abstract string ErrParentDirIndexNotFound(object? arg0);
    public abstract string ErrPartition0NotFound { get; }
    public abstract string ErrPartitionExceedsH3Capacity { get; }
    public abstract string ErrPartitionExceedsOriginalArea { get; }
    public abstract string ErrPartitionReadOutOfRange { get; }
    public abstract string ErrPatchApplyError(object? arg0);
    public abstract string ErrPatchCorruptedDataTruncated(object? arg0);
    public abstract string ErrPatchCorruptedRleTruncated(object? arg0);
    public abstract string ErrPatchCrc32Mismatch { get; }
    public abstract string ErrPatchDataEmpty { get; }
    public abstract string ErrPatchFileEmpty { get; }
    public abstract string ErrPatchFileNotFoundPath(object? arg0);
    public abstract string ErrPatchFileTooShort(object? arg0);
    public abstract string ErrPatchFilesNotOnDisc(object? arg0, object? arg1);
    public abstract string ErrPatchFolderOrZipNotFound { get; }
    public abstract string ErrPatchFormatUnsupportedOrInvalid { get; }
    public abstract string ErrPatchGameIdMismatch(object? arg0, object? arg1);
    public abstract string ErrPatchInfoUnreadable { get; }
    public abstract string ErrPatchNotFound { get; }
    public abstract string ErrPatchPathEmpty { get; }
    public abstract string ErrPatchRecordOutOfSource(object? arg0, object? arg1, object? arg2);
    public abstract string ErrPatchReferencedFilesMissing(object? arg0, object? arg1);
    public abstract string ErrPatchStreamingOver2Gb { get; }
    public abstract string ErrPatchTargetMismatch(object? arg0);
    public abstract string ErrPathEmpty { get; }
    public abstract string ErrPathNotAllowed(object? arg0);
    public abstract string ErrPipeReaderCompleted { get; }
    public abstract string ErrPpfBlockcheckFailed(object? arg0);
    public abstract string ErrPpfSourceTooShortBlockcheck { get; }
    public abstract string ErrProgramRomfsNotFound { get; }
    public abstract string ErrPs1GamesOnly { get; }
    public abstract string ErrRawContentAbnormallyLarge(object? arg0, object? arg1);
    public abstract string ErrReadFailedDetail(object? arg0, object? arg1, object? arg2, object? arg3, object? arg4, object? arg5);
    public abstract string ErrRecompressedExceedsTitleBlock(object? arg0, object? arg1);
    public abstract string ErrRequiredHeaderNotFound(object? arg0);
    public abstract string ErrResolvedNameTooLong(object? arg0, object? arg1);
    public abstract string ErrRiivolutionXmlNotInFolder { get; }
    public abstract string ErrRiivolutionXmlOrFolderNotFound { get; }
    public abstract string ErrRomfsDataLengthMismatch(object? arg0, object? arg1);
    public abstract string ErrRomfsDirHashBucketInvalid { get; }
    public abstract string ErrRomfsFileDataTooShort(object? arg0);
    public abstract string ErrRomfsHeaderSizeInvalid(object? arg0);
    public abstract string ErrRomfsOriginalNotFound(object? arg0);
    public abstract string ErrRomfsPatchNoOriginalReader(object? arg0);
    public abstract string ErrRvzChunkSizeInvalid { get; }
    public abstract string ErrRvzChunkSizeInvalidValue(object? arg0);
    public abstract string ErrRvzChunkSizeMismatch { get; }
    public abstract string ErrRvzChunkTooLarge { get; }
    public abstract string ErrRvzCompressorDataSizeInvalid { get; }
    public abstract string ErrRvzDataNotFilled { get; }
    public abstract string ErrRvzDecompressResultNotFound { get; }
    public abstract string ErrRvzFileEndedEarly { get; }
    public abstract string ErrRvzFileTooLarge { get; }
    public abstract string ErrRvzFileTooSmall { get; }
    public abstract string ErrRvzGapBetweenDataRegions { get; }
    public abstract string ErrRvzGroupIndexOutOfRange { get; }
    public abstract string ErrRvzGroupOffsetInvalid { get; }
    public abstract string ErrRvzGroupPositionOutOfFile { get; }
    public abstract string ErrRvzHashExceptionIndexInvalid { get; }
    public abstract string ErrRvzHashExceptionListInvalid { get; }
    public abstract string ErrRvzHashExceptionOffsetInvalid { get; }
    public abstract string ErrRvzHeader1HashMismatch { get; }
    public abstract string ErrRvzHeader2HashMismatch { get; }
    public abstract string ErrRvzHeader2SizeInvalid { get; }
    public abstract string ErrRvzNotWiiForWbfs { get; }
    public abstract string ErrRvzPackDataInvalid { get; }
    public abstract string ErrRvzPackResultTooLarge { get; }
    public abstract string ErrRvzPackResultTooSmall { get; }
    public abstract string ErrRvzPackSeedInvalid { get; }
    public abstract string ErrRvzPackSizeMismatch { get; }
    public abstract string ErrRvzPartitionDataEntryNotFound { get; }
    public abstract string ErrRvzPartitionDataGroupsMissing { get; }
    public abstract string ErrRvzPartitionEntrySizeZero { get; }
    public abstract string ErrRvzPartitionSectorRangeInvalid { get; }
    public abstract string ErrRvzPartitionTableHashMismatch { get; }
    public abstract string ErrRvzPartitionTablePositionInvalid { get; }
    public abstract string ErrRvzRegionsNotContiguous(object? arg0, object? arg1);
    public abstract string ErrRvzSourceGroupsMissing { get; }
    public abstract string ErrRvzTableDecompressedSizeInvalid { get; }
    public abstract string ErrRvzTablePositionInvalid { get; }
    public abstract string ErrSaturnGameUnsupported { get; }
    public abstract string ErrSdRootEmpty { get; }
    public abstract string ErrSectorSizeTooSmall(object? arg0, object? arg1);
    public abstract string ErrSeekTableMismatch(object? arg0, object? arg1);
    public abstract string ErrSelectCueChd { get; }
    public abstract string ErrSelectOutputFolder { get; }
    public abstract string ErrSelectOutputFormat { get; }
    public abstract string ErrSelectSourceDiscFile { get; }
    public abstract string ErrSelectSourceFile { get; }
    public abstract string ErrSelectSourceSwitchFile { get; }
    public abstract string ErrSelectTitleToSplit { get; }
    public abstract string ErrSelectWorkFolder { get; }
    public abstract string ErrSfoMagicInvalid { get; }
    public abstract string ErrSfoTooSmall { get; }
    public abstract string ErrSlotKeyXMissing(object? arg0);
    public abstract string ErrSlotNormalKeyMissing(object? arg0);
    public abstract string ErrSoundUnitHeaderInvalid { get; }
    public abstract string ErrSourceBufferOpenFailed(object? arg0);
    public abstract string ErrSourceCrc32Mismatch(object? arg0, object? arg1);
    public abstract string ErrSourceFileNotFoundPath(object? arg0);
    public abstract string ErrSourceFileReadError { get; }
    public abstract string ErrSourceSizeMismatch(object? arg0, object? arg1);
    public abstract string ErrSparseCannotUnpack { get; }
    public abstract string ErrSparseNcaNeedsUpdate { get; }
    public abstract string ErrSpecifyPatch { get; }
    public abstract string ErrTitleDbResourceMissing { get; }
    public abstract string ErrTitleDbVerifyFailed(object? arg0, object? arg1);
    public abstract string ErrTitleTikNotFound { get; }
    public abstract string ErrTitleTmdNotFound { get; }
    public abstract string ErrTmdContentIndexMissing(object? arg0);
    public abstract string ErrTmdFakeSignFailed { get; }
    public abstract string ErrTmdFileNotFound { get; }
    public abstract string ErrTmdFstContentNotFound { get; }
    public abstract string ErrToneCodingModeInvalid { get; }
    public abstract string ErrTonePositionInvalid { get; }
    public abstract string ErrToneQuantIndexInvalid { get; }
    public abstract string ErrTooManyContentRecords(object? arg0);
    public abstract string ErrTooManyGroups { get; }
    public abstract string ErrTrackFileCreateFailed(object? arg0);
    public abstract string ErrTrackNoIndex01(object? arg0, object? arg1);
    public abstract string ErrTrackStreamEndedEarly { get; }
    public abstract string ErrUncompressedNacpLanguageLimit(object? arg0, object? arg1, object? arg2);
    public abstract string ErrUnexpectedEndOfRead { get; }
    public abstract string ErrUnicvDbEntryMagicInvalid(object? arg0, object? arg1, object? arg2);
    public abstract string ErrUnicvDbMagicInvalid { get; }
    public abstract string ErrUnicvDbNotFound { get; }
    public abstract string ErrUnknownCcdTrackMode(object? arg0);
    public abstract string ErrUnknownSignatureType(object? arg0);
    public abstract string ErrUnpackDataOldOrCorrupt { get; }
    public abstract string ErrUnpackFolderInfoUnreadable { get; }
    public abstract string ErrUnpackFolderInvalid { get; }
    public abstract string ErrUnpackMetaMissing { get; }
    public abstract string ErrUnpackedFolderEntry { get; }
    public abstract string ErrUnpackedFolderEntryWiiU { get; }
    public abstract string ErrUnpackedProgramFolderNotFound { get; }
    public abstract string ErrUnsafePathInArchive(object? arg0);
    public abstract string ErrUnsafePathInDisc(object? arg0);
    public abstract string ErrUnsupportedArchiveType(object? arg0);
    public abstract string ErrUnsupportedAudioFormat(object? arg0);
    public abstract string ErrUnsupportedChdHunkSize { get; }
    public abstract string ErrUnsupportedChdTrackType(object? arg0);
    public abstract string ErrUnsupportedContentType(object? arg0);
    public abstract string ErrUnsupportedConversion(object? arg0, object? arg1);
    public abstract string ErrUnsupportedExtension(object? arg0);
    public abstract string ErrUnsupportedFileType(object? arg0);
    public abstract string ErrUnsupportedFormat(object? arg0);
    public abstract string ErrUnsupportedFormatGeneric { get; }
    public abstract string ErrUnsupportedFormatVersion(object? arg0, object? arg1);
    public abstract string ErrUnsupportedGdromTrackLayout { get; }
    public abstract string ErrUnsupportedOutputFormat(object? arg0);
    public abstract string ErrUnsupportedPatchFormat { get; }
    public abstract string ErrUnsupportedPatchFormatWii { get; }
    public abstract string ErrUnsupportedPpfVersion(object? arg0);
    public abstract string ErrUnsupportedRomFormat { get; }
    public abstract string ErrUnsupportedRvzCompression(object? arg0);
    public abstract string ErrUnsupportedSampleRate { get; }
    public abstract string ErrUnsupportedSoftwareType(object? arg0);
    public abstract string ErrUnsupportedSourceFormat(object? arg0);
    public abstract string ErrUnsupportedTmdSignatureType(object? arg0);
    public abstract string ErrUnsupportedTrackType(object? arg0);
    public abstract string ErrUnsupportedWiiCommonKeyIndex(object? arg0);
    public abstract string ErrUnsupportedWiiPartitionLayout { get; }
    public abstract string ErrUpdateCannotBuildCia { get; }
    public abstract string ErrUpdateCannotConvertToCci { get; }
    public abstract string ErrUpdateInfoCheckFailed(object? arg0);
    public abstract string ErrUpdateNotesDisplayFailed(object? arg0);
    public abstract string ErrUpdateSameAsBase(object? arg0);
    public abstract string ErrUseFlushAsync { get; }
    public abstract string ErrUserData2048Required { get; }
    public abstract string ErrVitaAddPkgFiles { get; }
    public abstract string ErrVitaAddSourceFiles { get; }
    public abstract string ErrVitaAppKeyNotFound(object? arg0, object? arg1);
    public abstract string ErrVitaContainerFoldersNotFound { get; }
    public abstract string ErrVitaDuplicatePkgKind(object? arg0, object? arg1);
    public abstract string ErrVitaItemCategoryMissing(object? arg0);
    public abstract string ErrVitaItemTitleIdMissing(object? arg0);
    public abstract string ErrVitaKeyRequired(object? arg0, object? arg1);
    public abstract string ErrVitaLicenseContentIdMismatch(object? arg0, object? arg1);
    public abstract string ErrVitaOverwriteFileInfoMissing { get; }
    public abstract string ErrVitaPatchSourceInfoMissing { get; }
    public abstract string ErrVitaPkgNoLicenseWorkBin { get; }
    public abstract string ErrVitaPkgOnlyConvertible { get; }
    public abstract string ErrVitaPkgProbeMissing(object? arg0);
    public abstract string ErrVitaSourceAccessorMissing { get; }
    public abstract string ErrVitaTitleIdFromContentId(object? arg0);
    public abstract string ErrVitaTitleWithoutApp { get; }
    public abstract string ErrVitaUnpackedEntryReaddSource { get; }
    public abstract string ErrVitaUnpackedFolderRepackOnly(object? arg0);
    public abstract string ErrVitaUnsupportedPkgContentType(object? arg0);
    public abstract string ErrVitaUnsupportedPkgKeyType(object? arg0);
    public abstract string ErrVitaUnsupportedSourceType(object? arg0);
    public abstract string ErrVitaZrifContentIdMismatch(object? arg0, object? arg1);
    public abstract string ErrWbfsBlockCountInvalid { get; }
    public abstract string ErrWbfsClusterOutOfFile { get; }
    public abstract string ErrWbfsFileEndedEarly { get; }
    public abstract string ErrWbfsNoDisc { get; }
    public abstract string ErrWbfsReadOutOfRange { get; }
    public abstract string ErrWbfsSectorSizeInvalid { get; }
    public abstract string ErrWbfsWlbaOutOfFile { get; }
    public abstract string ErrWiaReadOutOfRange { get; }
    public abstract string ErrWiaReadProgressFailed { get; }
    public abstract string ErrWiaZstdSpecified { get; }
    public abstract string ErrWiiChunkSizeRange { get; }
    public abstract string ErrWiiCompressionUnsupported { get; }
    public abstract string ErrWiiDiscInfoUnreadable { get; }
    public abstract string ErrWiiDiscMaxSizeExceeded { get; }
    public abstract string ErrWiiGroupBufferSizeInvalid { get; }
    public abstract string ErrWiiIsoOnlyForWbfs { get; }
    public abstract string ErrWiiPartitionBootInfoInvalid { get; }
    public abstract string ErrWiiPartitionCertOffsetInvalid { get; }
    public abstract string ErrWiiPartitionDataOverlap { get; }
    public abstract string ErrWiiPartitionHeaderOffsetInvalid { get; }
    public abstract string ErrWiiPartitionLayoutInvalid { get; }
    public abstract string ErrWiiPartitionNotFound { get; }
    public abstract string ErrWiiTicketSizeInvalid { get; }
    public abstract string ErrWiiUKeysNotFound { get; }
    public abstract string ErrWorkBinNoKlicensee(object? arg0);
    public abstract string ErrWorkBinTooSmall(object? arg0);
    public abstract string ErrWpfResourceNotFound(object? arg0);
    public abstract string ErrXd3GetSrcBlk { get; }
    public abstract string ErrXd3Input { get; }
    public abstract string ErrXd3Internal(object? arg0);
    public abstract string ErrXd3Invalid { get; }
    public abstract string ErrXd3InvalidInput { get; }
    public abstract string ErrXd3NoSecond { get; }
    public abstract string ErrXd3Output { get; }
    public abstract string ErrXd3Unimplemented { get; }
    public abstract string ErrXmlPathOutsideSdRoot(object? arg0);
    public abstract string ErrZipEntryMissing(object? arg0);
    public abstract string ErrZstdLevelOutOfRange { get; }
    public abstract string ErrorNoMetadata { get; }

    // Filter
    public abstract string FilterAllFiles { get; }
    public abstract string FilterArchiveFiles { get; }
    public abstract string FilterFilesOfType(object? arg0);
    public abstract string FilterKoreanPatchArchive { get; }
    public abstract string FilterPatchFiles { get; }
    public abstract string FilterPc98DiskImage { get; }
    public abstract string FilterRiivolutionPatch { get; }
    public abstract string FilterRomFiles3ds { get; }
    public abstract string FilterSupportedFiles { get; }
    public abstract string FilterSwitchFiles { get; }
    public abstract string FilterWiiDiscFiles { get; }

    // Header
    public abstract string HeaderCompressionRatio { get; }
    public abstract string HeaderConvert { get; }
    public abstract string HeaderFileName { get; }
    public abstract string HeaderFileNameCompare { get; }
    public abstract string HeaderFilePath { get; }
    public abstract string HeaderForcedLanguage { get; }
    public abstract string HeaderFormat { get; }
    public abstract string HeaderInput { get; }
    public abstract string HeaderKey { get; }
    public abstract string HeaderLocalTitle { get; }
    public abstract string HeaderOutput { get; }
    public abstract string HeaderPatch { get; }
    public abstract string HeaderProductCode { get; }
    public abstract string HeaderResult { get; }
    public abstract string HeaderSize { get; }
    public abstract string HeaderTitle { get; }
    public abstract string HeaderVersion { get; }

    // Hint
    public abstract string HintDragOrClickPatch3ds { get; }
    public abstract string HintDragOrClickPatchFile { get; }
    public abstract string HintDragOrClickPatchFolderIpsZip { get; }
    public abstract string HintDragOrClickPatchFolderZip { get; }
    public abstract string HintDragOrClickRiivolutionPatch { get; }
    public abstract string HintDragOrClickRom3ds { get; }
    public abstract string HintDragOrClickSourceFile { get; }
    public abstract string HintDragOrClickSourceHdi { get; }
    public abstract string HintDragOrClickSourceZip { get; }
    public abstract string HintDragOrClickWiiDisc { get; }
    public abstract string HintDragOrSelectOutputFolder { get; }
    public abstract string HintDragOrSelectWorkFolder { get; }
    public abstract string HintDropArcadeFiles { get; }
    public abstract string HintDropCueChd { get; }
    public abstract string HintDropFile { get; }
    public abstract string HintDropFileOrFolder { get; }
    public abstract string HintDropGameFileOrFolder { get; }
    public abstract string HintDropImageFile { get; }
    public abstract string HintDropOrClickAudio { get; }
    public abstract string HintDropPatchFile { get; }
    public abstract string HintDropSourceNsp { get; }
    public abstract string HintDropVitaFiles { get; }
    public abstract string HintDropVitaPkg { get; }
    public abstract string HintDropWiiUFiles { get; }
    public abstract string HintKeyFileEmptyUsesExeFolder(object? arg0);
    public abstract string HintRepackReplaceRule { get; }
    public abstract string HintSearchGame { get; }
    public abstract string HintSelectOutput { get; }
    public abstract string HintSoundCancel { get; }
    public abstract string HintSoundComplete { get; }
    public abstract string HintSoundError { get; }
    public abstract string HintSplitExpandRule { get; }

    // Label
    public abstract string LabelActual { get; }
    public abstract string LabelAlgorithm { get; }
    public abstract string LabelAllFormats { get; }
    public abstract string LabelBackgroundMusic { get; }
    public abstract string LabelBootLogo { get; }
    public abstract string LabelCalculating { get; }
    public abstract string LabelCertsDropOrSelectFile { get; }
    public abstract string LabelCompression { get; }
    public abstract string LabelCompressionAlgorithmIso { get; }
    public abstract string LabelCompressionLevel { get; }
    public abstract string LabelCompressionLevelNszXcz { get; }
    public abstract string LabelCompressionMode { get; }
    public abstract string LabelCrcMatch { get; }
    public abstract string LabelCrcMismatch { get; }
    public abstract string LabelDatPatchNotRegistered(object? arg0);
    public abstract string LabelDefaultImage { get; }
    public abstract string LabelDlcPatch(object? arg0);
    public abstract string LabelDownloadLocation { get; }
    public abstract string LabelEstimated { get; }
    public abstract string LabelExpectedFileName { get; }
    public abstract string LabelExperimental { get; }
    public abstract string LabelFile { get; }
    public abstract string LabelFileCount(object? arg0);
    public abstract string LabelFileCountN(object? arg0);
    public abstract string LabelFolder { get; }
    public abstract string LabelFormat3dsCci { get; }
    public abstract string LabelFormatBinCueSingleTrack { get; }
    public abstract string LabelFormatChdIsoBased { get; }
    public abstract string LabelFormatList3dsCciCia { get; }
    public abstract string LabelFormatListIsoStandard { get; }
    public abstract string LabelFormatListLoadiineFolderWua { get; }
    public abstract string LabelFormatListWuaWupLoadiine { get; }
    public abstract string LabelFormatListWupFolderWua { get; }
    public abstract string LabelFormatListWupLoadiine { get; }
    public abstract string LabelGameId { get; }
    public abstract string LabelGameName { get; }
    public abstract string LabelGeneral { get; }
    public abstract string LabelKeyFileFoundInExe { get; }
    public abstract string LabelKeyFileMissingUsingExe { get; }
    public abstract string LabelKeyFileNone { get; }
    public abstract string LabelKeyFileSpecifiedNotFound { get; }
    public abstract string LabelKeyFileUsingSpecified { get; }
    public abstract string LabelKoreanPatch { get; }
    public abstract string LabelLanguage { get; }
    public abstract string LabelLocaleEnglish { get; }
    public abstract string LabelLocaleNone { get; }
    public abstract string LabelLocalePortuguese { get; }
    public abstract string LabelLocaleSpanish { get; }
    public abstract string LabelLz4Compression { get; }
    public abstract string LabelMakerCode(object? arg0);
    public abstract string LabelMergeMode { get; }
    public abstract string LabelMultiRomIndex { get; }
    public abstract string LabelNamingDigits(object? arg0);
    public abstract string LabelNamingFullName { get; }
    public abstract string LabelNone { get; }
    public abstract string LabelNoteCemuArchive { get; }
    public abstract string LabelNoteDiscImage { get; }
    public abstract string LabelNoteFolder { get; }
    public abstract string LabelNspTitleId { get; }
    public abstract string LabelOptions { get; }
    public abstract string LabelOther { get; }
    public abstract string LabelOthers { get; }
    public abstract string LabelOutputEmu { get; }
    public abstract string LabelOutputFormat { get; }
    public abstract string LabelOutputOptions { get; }
    public abstract string LabelOutputRetail { get; }
    public abstract string LabelPatch { get; }
    public abstract string LabelPatchInfo { get; }
    public abstract string LabelPatchNumber(object? arg0);
    public abstract string LabelPbpPacking { get; }
    public abstract string LabelPkgCount(object? arg0);
    public abstract string LabelPlay { get; }
    public abstract string LabelPopsEmulatorSettings { get; }
    public abstract string LabelPublisher { get; }
    public abstract string LabelQuality { get; }
    public abstract string LabelReplacementFiles(object? arg0);
    public abstract string LabelSelectPatch { get; }
    public abstract string LabelSettings { get; }
    public abstract string LabelSmallFilesHidden(object? arg0);
    public abstract string LabelSoundVolume { get; }
    public abstract string LabelSource { get; }
    public abstract string LabelSourceAutoExtractFormats { get; }
    public abstract string LabelSourceBinFileName { get; }
    public abstract string LabelStop { get; }
    public abstract string LabelSupportedLanguages { get; }
    public abstract string LabelSupportedPatchFormats { get; }
    public abstract string LabelTargetGameId(object? arg0);
    public abstract string LabelTargetTitleId(object? arg0);
    public abstract string LabelTistoryUrl { get; }
    public abstract string LabelUnknown { get; }
    public abstract string LabelUnlimited { get; }
    public abstract string LabelVerifiedOnManyGames { get; }
    public abstract string LabelVersion { get; }
    public abstract string LabelWarningValue(object? arg0);
    public abstract string LabelWarningsCountShownInLog(object? arg0);
    public abstract string LabelWideScreen { get; }
    public abstract string LabelWiiDisc { get; }

    // Log
    public abstract string LogAddFailed(object? arg0, object? arg1);
    public abstract string LogAddFilesError(object? arg0);
    public abstract string LogAllDoneElapsed(object? arg0);
    public abstract string LogAlreadyDecrypted(object? arg0);
    public abstract string LogAnalyzeMetadata { get; }
    public abstract string LogArcadeCrcMismatchBlocked { get; }
    public abstract string LogArchiveCannotOpenPasswordOrCorrupt(object? arg0, object? arg1);
    public abstract string LogArchiveMismatchRetry { get; }
    public abstract string LogArchiveMultipleCandidatesFound(object? arg0);
    public abstract string LogArchiveOpenError(object? arg0);
    public abstract string LogBaseLz4Header(object? arg0, object? arg1, object? arg2, object? arg3);
    public abstract string LogBgmActionDone(object? arg0, object? arg1);
    public abstract string LogBgmActionError(object? arg0, object? arg1);
    public abstract string LogBgmActionStart(object? arg0, object? arg1);
    public abstract string LogBinCueExtractDone { get; }
    public abstract string LogBulkAssignDone(object? arg0);
    public abstract string LogBulkAssignFailed(object? arg0);
    public abstract string LogCancellingCompress { get; }
    public abstract string LogCancellingConversion { get; }
    public abstract string LogCannotAutoAddFolder(object? arg0);
    public abstract string LogCannotOpenKoreanPatchArchive(object? arg0);
    public abstract string LogCcdCompanionCopyError(object? arg0);
    public abstract string LogCcdNotFoundNotCdImage { get; }
    public abstract string LogCertsExtractDone(object? arg0);
    public abstract string LogCertsExtractError(object? arg0);
    public abstract string LogCertsExtractStart(object? arg0);
    public abstract string LogCertsFileTooSmall(object? arg0, object? arg1);
    public abstract string LogCertsIncompleteRead(object? arg0, object? arg1);
    public abstract string LogCertsOffsetSize(object? arg0, object? arg1);
    public abstract string LogCertsSuccess(object? arg0, object? arg1);
    public abstract string LogChainPatchApplying(object? arg0, object? arg1, object? arg2);
    public abstract string LogChdConvertDone(object? arg0);
    public abstract string LogCheckingSourceArchive(object? arg0);
    public abstract string LogCiaDirectBuildDone { get; }
    public abstract string LogCleanupException(object? arg0);
    public abstract string LogCleanupFailed(object? arg0);
    public abstract string LogCmdCreated(object? arg0);
    public abstract string LogCmdMissingSkipped(object? arg0);
    public abstract string LogCompressAndMerge { get; }
    public abstract string LogCompressAndSplit { get; }
    public abstract string LogCompressDone(object? arg0);
    public abstract string LogCompressStart(object? arg0);
    public abstract string LogCompressionRatio { get; }
    public abstract string LogContentFileMissingSkip(object? arg0);
    public abstract string LogContentIndexMissingSkip(object? arg0);
    public abstract string LogContentSaved(object? arg0);
    public abstract string LogConvertDone(object? arg0);
    public abstract string LogConvertFailed(object? arg0, object? arg1);
    public abstract string LogConvertStart(object? arg0);
    public abstract string LogConvertStartTo(object? arg0, object? arg1);
    public abstract string LogConvertToCciStart(object? arg0);
    public abstract string LogConvertToCiaStart(object? arg0);
    public abstract string LogCreatingTitleDbBackup(object? arg0);
    public abstract string LogCueAnalyzed(object? arg0, object? arg1);
    public abstract string LogCueCreated(object? arg0);
    public abstract string LogCueItemFailed(object? arg0, object? arg1);
    public abstract string LogCueNotFoundNotCdImage { get; }
    public abstract string LogCueProcessError(object? arg0);
    public abstract string LogCueSkipExists(object? arg0);
    public abstract string LogCueStart(object? arg0);
    public abstract string LogDcpSourceMissing(object? arg0);
    public abstract string LogDecompressAndMerge { get; }
    public abstract string LogDecompressDone(object? arg0);
    public abstract string LogDecompressDoneName(object? arg0);
    public abstract string LogDecompressStart(object? arg0);
    public abstract string LogDecompressingName(object? arg0);
    public abstract string LogDecryptFailed(object? arg0, object? arg1);
    public abstract string LogDeleteIncompleteFailed(object? arg0);
    public abstract string LogDeleteIncompleteFile { get; }
    public abstract string LogDeletingInstalledTitleFolder { get; }
    public abstract string LogDiscInfoReadFailed(object? arg0);
    public abstract string LogDlcAllDone(object? arg0, object? arg1);
    public abstract string LogDlcBuildStart(object? arg0, object? arg1);
    public abstract string LogDlcDone(object? arg0);
    public abstract string LogDlcFailed(object? arg0, object? arg1);
    public abstract string LogDone(object? arg0);
    public abstract string LogDoneTotalTime(object? arg0);
    public abstract string LogDownloadSummary(object? arg0, object? arg1);
    public abstract string LogDuplicateIpsSkipped(object? arg0);
    public abstract string LogEmptySaveCreated(object? arg0);
    public abstract string LogEncryptedContentDetected(object? arg0);
    public abstract string LogEncryptedRomDetected { get; }
    public abstract string LogEntryAlreadyCompressedSkipped(object? arg0);
    public abstract string LogEntryNcaMissingSkipped(object? arg0);
    public abstract string LogEntryNoRomfsSkipped(object? arg0);
    public abstract string LogEntrySparseUnsupportedSkipped(object? arg0);
    public abstract string LogError(object? arg0);
    public abstract string LogErrorWithTitle(object? arg0, object? arg1);
    public abstract string LogExefsFileReplaced(object? arg0, object? arg1, object? arg2);
    public abstract string LogExefsIconEntryNotFound { get; }
    public abstract string LogExefsIpsApplied(object? arg0, object? arg1, object? arg2);
    public abstract string LogExefsIpsFound(object? arg0);
    public abstract string LogExefsPatchApplied(object? arg0);
    public abstract string LogExefsPatchAppliedTarget(object? arg0, object? arg1, object? arg2);
    public abstract string LogExefsTargetNsoNotFound(object? arg0);
    public abstract string LogExheaderReplaced { get; }
    public abstract string LogExtdataIdUnknown(object? arg0);
    public abstract string LogExtractCancelled { get; }
    public abstract string LogExtractCciDone(object? arg0);
    public abstract string LogExtractDone(object? arg0);
    public abstract string LogExtractReady(object? arg0, object? arg1);
    public abstract string LogExtractStarted(object? arg0);
    public abstract string LogExtracting(object? arg0);
    public abstract string LogExtractingBinCue { get; }
    public abstract string LogExtractingCci(object? arg0);
    public abstract string LogExtractingLabel(object? arg0);
    public abstract string LogFailedCleaningFiles { get; }
    public abstract string LogFailedCount(object? arg0);
    public abstract string LogFailedWithMessage(object? arg0);
    public abstract string LogFileAnalyzeFailed(object? arg0, object? arg1);
    public abstract string LogFileDeleteFailed(object? arg0, object? arg1);
    public abstract string LogFileLoaded(object? arg0);
    public abstract string LogFileReplacedDetail(object? arg0, object? arg1, object? arg2);
    public abstract string LogFilesReplacedTotal(object? arg0, object? arg1);
    public abstract string LogFilesUnpackedCount(object? arg0);
    public abstract string LogFinalId(object? arg0, object? arg1);
    public abstract string LogFolderReadFailed(object? arg0, object? arg1);
    public abstract string LogGameIdFailedExcluded(object? arg0);
    public abstract string LogGameIdRecognizeError { get; }
    public abstract string LogGameIdRecognizeFailed(object? arg0, object? arg1);
    public abstract string LogGameInfoChanged { get; }
    public abstract string LogGameInfoLoadFailed(object? arg0);
    public abstract string LogGdiNotFoundNotGdrom { get; }
    public abstract string LogGdiProcessError(object? arg0);
    public abstract string LogHashItemDone(object? arg0, object? arg1);
    public abstract string LogHashItemFailed(object? arg0);
    public abstract string LogHashMismatch(object? arg0);
    public abstract string LogHashStart(object? arg0, object? arg1);
    public abstract string LogHdiVolumeAnalyzed { get; }
    public abstract string LogHomebrewFinalizeHint { get; }
    public abstract string LogInstallCancelled { get; }
    public abstract string LogInvalidTitleIdConfigured { get; }
    public abstract string LogItemAlreadyDoneSkipped(object? arg0, object? arg1, object? arg2);
    public abstract string LogItemBinCueToChd(object? arg0);
    public abstract string LogItemCcdToBinCue(object? arg0);
    public abstract string LogItemConvertFailed(object? arg0, object? arg1);
    public abstract string LogItemError(object? arg0, object? arg1);
    public abstract string LogItemUnsupportedFormat(object? arg0);
    public abstract string LogJobCancelled { get; }
    public abstract string LogJobDone(object? arg0);
    public abstract string LogJobStartPacking(object? arg0, object? arg1, object? arg2);
    public abstract string LogJobSummary(object? arg0, object? arg1);
    public abstract string LogJobsCompleted(object? arg0);
    public abstract string LogJobsStarting(object? arg0);
    public abstract string LogJobsStartingConvert(object? arg0);
    public abstract string LogJobsStartingDecrypt(object? arg0);
    public abstract string LogJobsStartingIntegratedConvert(object? arg0);
    public abstract string LogJobsStartingUnpack(object? arg0);
    public abstract string LogKoreanPatchPathNotFound(object? arg0);
    public abstract string LogLabelExtractDone(object? arg0, object? arg1);
    public abstract string LogLinkingBinCueStreaming { get; }
    public abstract string LogLinkingChdStreaming { get; }
    public abstract string LogLocaleFixSkippedNoRegion { get; }
    public abstract string LogLocaleFixed(object? arg0, object? arg1);
    public abstract string LogLz4Composition(object? arg0, object? arg1);
    public abstract string LogLz4CompressDone(object? arg0);
    public abstract string LogLz4CompressStart(object? arg0);
    public abstract string LogLz4NcaCompressing(object? arg0, object? arg1, object? arg2, object? arg3);
    public abstract string LogMainContentReadFailed(object? arg0, object? arg1);
    public abstract string LogMaxItemsExceeded(object? arg0, object? arg1);
    public abstract string LogMergeCancelled { get; }
    public abstract string LogMergeComplete(object? arg0);
    public abstract string LogMergeFailed(object? arg0, object? arg1);
    public abstract string LogMerging { get; }
    public abstract string LogModeCompress { get; }
    public abstract string LogModeDecompress { get; }
    public abstract string LogMountPathDetected(object? arg0);
    public abstract string LogMountPathRoot { get; }
    public abstract string LogMultiTrackFileNotFound(object? arg0);
    public abstract string LogNoFilesAdded { get; }
    public abstract string LogNoFilesToRename { get; }
    public abstract string LogNoProcessableFiles { get; }
    public abstract string LogNoSuccessfulJobs { get; }
    public abstract string LogNoTitleInfoItems { get; }
    public abstract string LogNotFoundInHdi(object? arg0);
    public abstract string LogNotInDisc(object? arg0);
    public abstract string LogOutputFile(object? arg0);
    public abstract string LogOutputPath(object? arg0);
    public abstract string LogOutputPathAndFormat(object? arg0, object? arg1);
    public abstract string LogPackagingToWup(object? arg0, object? arg1, object? arg2);
    public abstract string LogPartitionUnpackDone(object? arg0);
    public abstract string LogPasswordInputCancelled { get; }
    public abstract string LogPatchAddedCount(object? arg0, object? arg1);
    public abstract string LogPatchApplyingArrow(object? arg0, object? arg1);
    public abstract string LogPatchAssignInfoGeneric(object? arg0);
    public abstract string LogPatchAssignWarnGeneric(object? arg0);
    public abstract string LogPatchAssigned(object? arg0);
    public abstract string LogPatchCancelledName(object? arg0);
    public abstract string LogPatchCoverage(object? arg0, object? arg1, object? arg2);
    public abstract string LogPatchDoneDetail(object? arg0, object? arg1, object? arg2, object? arg3);
    public abstract string LogPatchDoneName(object? arg0);
    public abstract string LogPatchDoneWithTime(object? arg0, object? arg1);
    public abstract string LogPatchFailedMsg(object? arg0);
    public abstract string LogPatchFailedNamed(object? arg0, object? arg1);
    public abstract string LogPatchFailedNoOutput(object? arg0);
    public abstract string LogPatchFileAdded(object? arg0, object? arg1);
    public abstract string LogPatchFileReplaced(object? arg0, object? arg1);
    public abstract string LogPatchInfoRead(object? arg0);
    public abstract string LogPatchInfoReadWii(object? arg0, object? arg1);
    public abstract string LogPatchOverwrite(object? arg0, object? arg1);
    public abstract string LogPatchReadCancelled { get; }
    public abstract string LogPatchReadFailed(object? arg0);
    public abstract string LogPatchReplacedCount(object? arg0, object? arg1);
    public abstract string LogPatchResultSummary(object? arg0, object? arg1, object? arg2);
    public abstract string LogPatchSourceFolderDetected(object? arg0, object? arg1, object? arg2);
    public abstract string LogPatchSourceMismatchSkipped(object? arg0, object? arg1, object? arg2);
    public abstract string LogPatchStartName(object? arg0);
    public abstract string LogPatchSucceededCount(object? arg0, object? arg1, object? arg2);
    public abstract string LogPatchTargetMissing { get; }
    public abstract string LogPatchingTmdHash { get; }
    public abstract string LogPc98MissingFilesSkipped(object? arg0);
    public abstract string LogPc98PatchDone(object? arg0, object? arg1);
    public abstract string LogPlanLooseCandidates(object? arg0, object? arg1);
    public abstract string LogPlanPatchDone(object? arg0, object? arg1);
    public abstract string LogPlanReplaceDone(object? arg0, object? arg1);
    public abstract string LogPreviewPlaybackError(object? arg0);
    public abstract string LogPreviewPrepareFailed(object? arg0);
    public abstract string LogProcessError { get; }
    public abstract string LogProcessStart { get; }
    public abstract string LogProgramBuildDone(object? arg0, object? arg1);
    public abstract string LogProgramBuildStart(object? arg0);
    public abstract string LogRecoveryCancelled { get; }
    public abstract string LogRecoveryRegistered(object? arg0);
    public abstract string LogRecoverySkipped(object? arg0);
    public abstract string LogRecoveryStarting { get; }
    public abstract string LogReinstallDeclined { get; }
    public abstract string LogRenameDone(object? arg0);
    public abstract string LogRenameDoneWithFailures(object? arg0, object? arg1);
    public abstract string LogRepackStart { get; }
    public abstract string LogRepackStartTarget(object? arg0);
    public abstract string LogReplaced(object? arg0);
    public abstract string LogReplacedArrow(object? arg0, object? arg1);
    public abstract string LogReplacingExistingKind(object? arg0);
    public abstract string LogResultSaved(object? arg0);
    public abstract string LogRomfsFileReplaced(object? arg0);
    public abstract string LogRomfsLz4Off { get; }
    public abstract string LogRomfsLz4On { get; }
    public abstract string LogRomfsPatchApplied(object? arg0);
    public abstract string LogScanStarting { get; }
    public abstract string LogSdAndMovablePaths(object? arg0, object? arg1);
    public abstract string LogSkipAlreadyLz4 { get; }
    public abstract string LogSkipBaseWithUpdate { get; }
    public abstract string LogSkipOldVersion { get; }
    public abstract string LogSkipUnsupportedFormat(object? arg0);
    public abstract string LogSkipUpdateAlone { get; }
    public abstract string LogSkippedCountReasons(object? arg0);
    public abstract string LogSomeProgramsUnpatched { get; }
    public abstract string LogSourceArchiveExtracted(object? arg0);
    public abstract string LogSparseNotAddedToList(object? arg0);
    public abstract string LogSplitCancelled { get; }
    public abstract string LogSplitComplete(object? arg0);
    public abstract string LogSplitFailed(object? arg0, object? arg1);
    public abstract string LogSplitPreparing(object? arg0);
    public abstract string LogSplitSkipNoTitleInfo(object? arg0);
    public abstract string LogSplitting { get; }
    public abstract string LogStaleResultDeleteFailed(object? arg0, object? arg1);
    public abstract string LogStatusCompressing { get; }
    public abstract string LogStatusCopying { get; }
    public abstract string LogStatusDecompressing { get; }
    public abstract string LogStatusDone { get; }
    public abstract string LogStatusUnpacking { get; }
    public abstract string LogStatusValidating { get; }
    public abstract string LogStreamingConvertStart { get; }
    public abstract string LogStreamingRepackStart { get; }
    public abstract string LogStreamingRepackStartTarget(object? arg0);
    public abstract string LogTempExtractCleaned { get; }
    public abstract string LogTempExtractCleanupFailed(object? arg0);
    public abstract string LogTempFileDeleted(object? arg0);
    public abstract string LogTistoryAllFinished { get; }
    public abstract string LogTistoryAnalyzeCancelled { get; }
    public abstract string LogTistoryAnalyzeDone(object? arg0);
    public abstract string LogTistoryAnalyzeFailed(object? arg0);
    public abstract string LogTistoryAnalyzeStart(object? arg0);
    public abstract string LogTistoryDownloadStart(object? arg0);
    public abstract string LogTistoryNoNewAttachments { get; }
    public abstract string LogTistoryUnzipItemDone(object? arg0);
    public abstract string LogTistoryUnzipItemFailed(object? arg0, object? arg1);
    public abstract string LogTistoryUnzipSkippedPartial(object? arg0);
    public abstract string LogTistoryUnzipStart { get; }
    public abstract string LogTistoryUnzipSummary(object? arg0, object? arg1, object? arg2);
    public abstract string LogTitleAlreadyExists(object? arg0);
    public abstract string LogTitleGroupDetected(object? arg0);
    public abstract string LogTitleIdOverride(object? arg0);
    public abstract string LogTitleListLoaded { get; }
    public abstract string LogTitlesAdded(object? arg0, object? arg1);
    public abstract string LogTmdInvalidSkipped(object? arg0);
    public abstract string LogTmdReadFailed(object? arg0, object? arg1);
    public abstract string LogTmdSaved(object? arg0);
    public abstract string LogTrackFileNotFound(object? arg0);
    public abstract string LogTryingPatchOnArchive { get; }
    public abstract string LogUnpackDone { get; }
    public abstract string LogUnpackDoneFilesTo(object? arg0, object? arg1);
    public abstract string LogUnpackDoneTime(object? arg0);
    public abstract string LogUnpackFolderInfoReadFailed(object? arg0);
    public abstract string LogUnpackFolderItemRemoved { get; }
    public abstract string LogUnpackFolderLoaded(object? arg0);
    public abstract string LogUnpackFolderLoadedTitles(object? arg0);
    public abstract string LogUnpackStart { get; }
    public abstract string LogUnpackStartingTo(object? arg0);
    public abstract string LogUnpackingPath(object? arg0);
    public abstract string LogUnpackingTitle(object? arg0, object? arg1, object? arg2);
    public abstract string LogUnsupportedPatchFormatNamed(object? arg0);
    public abstract string LogUnsupportedWiiUInputFormat(object? arg0);
    public abstract string LogUpdatingTitleDb { get; }
    public abstract string LogUrlProcessFailed(object? arg0, object? arg1);
    public abstract string LogValidationComplete { get; }
    public abstract string LogValidationStart { get; }
    public abstract string LogVitaAllDoneTitles(object? arg0, object? arg1);
    public abstract string LogVitaConvertingNoNpDrm(object? arg0);
    public abstract string LogVitaCreatingPatch(object? arg0);
    public abstract string LogVitaDecryptFailed(object? arg0, object? arg1, object? arg2);
    public abstract string LogVitaFileListCheckFailed(object? arg0, object? arg1, object? arg2);
    public abstract string LogVitaFolderRepacking(object? arg0);
    public abstract string LogVitaItemProcessFailed(object? arg0, object? arg1, object? arg2);
    public abstract string LogVitaMergingGamePatch(object? arg0);
    public abstract string LogVitaOnlyPkgAddable(object? arg0);
    public abstract string LogVitaPatchAllDone(object? arg0, object? arg1, object? arg2, object? arg3);
    public abstract string LogVitaPatchFailedKeptOriginal(object? arg0, object? arg1, object? arg2);
    public abstract string LogVitaPatchInfoReadDetail(object? arg0, object? arg1, object? arg2);
    public abstract string LogVitaPatchOnlyDone(object? arg0, object? arg1, object? arg2, object? arg3);
    public abstract string LogVitaPatchOnlyDuplicateSkipped(object? arg0, object? arg1);
    public abstract string LogVitaPatchPartial(object? arg0, object? arg1, object? arg2, object? arg3, object? arg4);
    public abstract string LogVitaPatchSharesAppLicense(object? arg0);
    public abstract string LogVitaPkgGroupOutput(object? arg0, object? arg1, object? arg2);
    public abstract string LogVitaRepackDone(object? arg0, object? arg1, object? arg2, object? arg3, object? arg4);
    public abstract string LogVitaReplacedDuplicate(object? arg0, object? arg1, object? arg2, object? arg3);
    public abstract string LogVitaReplacedExisting(object? arg0, object? arg1, object? arg2);
    public abstract string LogVitaReplacedSameDlc(object? arg0, object? arg1);
    public abstract string LogVitaSharingAppKey(object? arg0, object? arg1);
    public abstract string LogVitaSourceReadFailed(object? arg0, object? arg1, object? arg2);
    public abstract string LogVitaUnpackGroupDone(object? arg0, object? arg1, object? arg2);
    public abstract string LogVitaWorkBinMissingSkipped(object? arg0, object? arg1);
    public abstract string LogWorkCancelled(object? arg0, object? arg1);
    public abstract string LogWorkDone(object? arg0, object? arg1);
    public abstract string LogWorkFailedCode(object? arg0, object? arg1);
    public abstract string LogWorkStart(object? arg0, object? arg1);
    public abstract string LogXdeltaApplied(object? arg0, object? arg1, object? arg2);
    public abstract string LogXdeltaTargetsDetected(object? arg0);
    public abstract string LogZcciDetectedStreaming { get; }

    // Lz4
    public abstract string Lz4EstimateFailed { get; }
    public abstract string Lz4StatusCompressed { get; }

    // Main
    public abstract string MainErrNoFiles { get; }
    public abstract string MainErrNoKeys { get; }
    public abstract string MainErrNoOutput { get; }
    public abstract string MainLogAllComplete(object? arg0);
    public abstract string MainLogRecalculating { get; }
    public abstract string MainMsgDone { get; }
    public abstract string MainMsgSplitDone { get; }

    // Menu
    public abstract string MenuClear { get; }
    public abstract string MenuClearLog { get; }
    public abstract string MenuCopyLog { get; }
    public abstract string MenuDelete { get; }
    public abstract string MenuDeselect { get; }
    public abstract string MenuExtractCci { get; }
    public abstract string MenuExtractCia { get; }
    public abstract string MenuFindInArchive { get; }
    public abstract string MenuFindInFolder { get; }
    public abstract string MenuOpenFolder { get; }
    public abstract string MenuRemoveSelected { get; }
    public abstract string MenuSelectArchive { get; }
    public abstract string MenuSelectFileIpsZip { get; }
    public abstract string MenuSelectFileZip7z { get; }
    public abstract string MenuSelectFileZip7zXml { get; }
    public abstract string MenuSelectFolder { get; }

    // Message
    public abstract string MsgArchiveMultipleCandidatesSelect { get; }
    public abstract string MsgArchiveOpenFailed { get; }
    public abstract string MsgBulkAssignDetails(object? arg0);
    public abstract string MsgBulkAssignSummary(object? arg0, object? arg1);
    public abstract string MsgCancelled { get; }
    public abstract string MsgChdGdiCueBasisExplain { get; }
    public abstract string MsgCloseDefaultsToGdi { get; }
    public abstract string MsgConfirmCancelAndExit { get; }
    public abstract string MsgConfirmDiscardUnpacked { get; }
    public abstract string MsgConfirmRebuildTitleDb { get; }
    public abstract string MsgConfirmReinstall(object? arg0);
    public abstract string MsgForcedLanguageCancelled { get; }
    public abstract string MsgForcedLanguageSet(object? arg0);
    public abstract string MsgGdromChdSelectHashBasis { get; }
    public abstract string MsgImageLoadFailed(object? arg0);
    public abstract string MsgNo3dsKeyFilesDesc { get; }
    public abstract string MsgNo3dsKeyFilesTitle { get; }
    public abstract string MsgNoProdKeysTitle { get; }
    public abstract string MsgNoWiiUKeysDesc { get; }
    public abstract string MsgNoWiiUKeysTitle { get; }
    public abstract string MsgPasswordWrongRetry { get; }
    public abstract string MsgPatchAssignFailedNoMatch(object? arg0, object? arg1, object? arg2);
    public abstract string MsgPatchAssignFailedNotFound(object? arg0);
    public abstract string MsgPatchDiscGameIdMismatch { get; }
    public abstract string MsgPatchDiscMatches { get; }
    public abstract string MsgPatchPathMustBeFolderZip7zXml { get; }
    public abstract string MsgPatchRomMatches { get; }
    public abstract string MsgPatchRomMismatch { get; }
    public abstract string MsgPatchSpecifyDiscToVerify { get; }
    public abstract string MsgPatchSpecifyRomToVerify { get; }
    public abstract string MsgTitleBulkAssignResult { get; }
    public abstract string MsgTitleError { get; }
    public abstract string MsgTitleInfo { get; }
    public abstract string MsgTitlePatchAssignFailed { get; }
    public abstract string MsgTitleQuestion { get; }
    public abstract string MsgTitleWarning { get; }

    // Naming
    public abstract string NamingTokDateDesc { get; }
    public abstract string NamingTokDateLabel { get; }
    public abstract string NamingTokDisplayVersionDesc { get; }
    public abstract string NamingTokDisplayVersionLabel { get; }
    public abstract string NamingTokFileNameDesc { get; }
    public abstract string NamingTokKTitleDesc { get; }
    public abstract string NamingTokKTitleLabel { get; }
    public abstract string NamingTokNameDesc { get; }
    public abstract string NamingTokNameLabel { get; }
    public abstract string NamingTokPatchLangDesc { get; }
    public abstract string NamingTokPatchLangLabel { get; }
    public abstract string NamingTokPatchVersionDesc { get; }
    public abstract string NamingTokRawVersionDesc { get; }
    public abstract string NamingTokSourceLangDesc { get; }
    public abstract string NamingTokSourceLangLabel { get; }
    public abstract string NamingTokTagDesc { get; }
    public abstract string NamingTokTagLabel { get; }
    public abstract string NamingTokTitleIdDesc { get; }
    public abstract string NamingTokTitleIdLabel { get; }
    public abstract string NamingTokTitleNameDesc { get; }

    // Note
    public abstract string NoteAssignedAsGenericPatch { get; }
    public abstract string NoteAssignedByArchiveName { get; }

    // Option
    public abstract string OptionChdmanLzma { get; }
    public abstract string OptionChdmanZlib { get; }
    public abstract string OptionChdmanZstd { get; }
    public abstract string OptionLevel0None { get; }
    public abstract string OptionLevel18Recommended { get; }
    public abstract string OptionLevel3Fastest { get; }
    public abstract string OptionLevel5Balanced { get; }
    public abstract string OptionLevel9Balanced { get; }
    public abstract string OptionLevel9Max { get; }

    // Progress
    public abstract string ProgressAnalyzingPatchInfo { get; }
    public abstract string ProgressApplyingExefsPatches(object? arg0);
    public abstract string ProgressApplyingLabel(object? arg0);
    public abstract string ProgressApplyingPatchCount(object? arg0, object? arg1);
    public abstract string ProgressApplyingPatchTo(object? arg0);
    public abstract string ProgressBuildingPfs0 { get; }
    public abstract string ProgressBuildingProgram { get; }
    public abstract string ProgressBuildingRomfs { get; }
    public abstract string ProgressCompletedCount(object? arg0, object? arg1);
    public abstract string ProgressCompressDone { get; }
    public abstract string ProgressCompressingNsz { get; }
    public abstract string ProgressCompressingRomfs { get; }
    public abstract string ProgressCompressingRvz(object? arg0);
    public abstract string ProgressCompressingWbfs(object? arg0);
    public abstract string ProgressCompressingZcci { get; }
    public abstract string ProgressConvertingBinCue { get; }
    public abstract string ProgressConvertingChd { get; }
    public abstract string ProgressConvertingFormat { get; }
    public abstract string ProgressConvertingIso { get; }
    public abstract string ProgressCopyingTrack(object? arg0, object? arg1);
    public abstract string ProgressCreatingIso(object? arg0);
    public abstract string ProgressCreatingIvfc(object? arg0, object? arg1);
    public abstract string ProgressElapsed(object? arg0);
    public abstract string ProgressExtractDone { get; }
    public abstract string ProgressExtractingIso { get; }
    public abstract string ProgressExtractingPatch { get; }
    public abstract string ProgressExtractingSource { get; }
    public abstract string ProgressFinalizingNsp { get; }
    public abstract string ProgressGdAligningSectors { get; }
    public abstract string ProgressGdAnalyzingTracks { get; }
    public abstract string ProgressGdApplyingXdelta(object? arg0);
    public abstract string ProgressGdCopyingTrack(object? arg0, object? arg1, object? arg2);
    public abstract string ProgressGdCreatingTrack(object? arg0, object? arg1);
    public abstract string ProgressGdExtractingDcp(object? arg0);
    public abstract string ProgressGdMergingSectors { get; }
    public abstract string ProgressGdParsingMetadata { get; }
    public abstract string ProgressGdPatchAllDone { get; }
    public abstract string ProgressGdPreparingMedia { get; }
    public abstract string ProgressGdRebuildAllDone { get; }
    public abstract string ProgressGdRelocatingIso9660(object? arg0, object? arg1);
    public abstract string ProgressGdTrackWritten { get; }
    public abstract string ProgressGdUpdatingIndex { get; }
    public abstract string ProgressGdWritingRawSectors { get; }
    public abstract string ProgressHashing { get; }
    public abstract string ProgressHashingNca { get; }
    public abstract string ProgressHashingPartitions(object? arg0);
    public abstract string ProgressHashingPercent(object? arg0);
    public abstract string ProgressLoadingCount(object? arg0, object? arg1);
    public abstract string ProgressLz4CompressingName(object? arg0);
    public abstract string ProgressPacking { get; }
    public abstract string ProgressPatchDone { get; }
    public abstract string ProgressPreparing { get; }
    public abstract string ProgressRepackingFile(object? arg0);
    public abstract string ProgressScanError(object? arg0, object? arg1);
    public abstract string ProgressScanningCount(object? arg0, object? arg1);
    public abstract string ProgressScanningTitle(object? arg0);
    public abstract string ProgressUnpackingFile(object? arg0);
    public abstract string ProgressUnpackingNcaEntry(object? arg0, object? arg1);
    public abstract string ProgressWritingRomfs { get; }
    public abstract string ProgressWupCreated(object? arg0, object? arg1);

    // Radio
    public abstract string RadioBlock { get; }
    public abstract string RadioBlockless { get; }
    public abstract string RadioCciEmulator { get; }
    public abstract string RadioCiaConsole { get; }
    public abstract string RadioConsoleRepatch { get; }
    public abstract string RadioEmulatorAppAddcont { get; }
    public abstract string RadioFolderPerTitleId { get; }
    public abstract string RadioLowercase { get; }
    public abstract string RadioMergeAll { get; }
    public abstract string RadioMergeGroupByTitleId { get; }
    public abstract string RadioOutputGameId { get; }
    public abstract string RadioOutputNormal(object? arg0);
    public abstract string RadioQuality30s { get; }
    public abstract string RadioQuality60s { get; }
    public abstract string RadioRvzDolphin { get; }
    public abstract string RadioUppercase { get; }
    public abstract string RadioWbfsUsbLoader { get; }
    public abstract string RadioWuaEmulator { get; }
    public abstract string RadioWupConsole { get; }
    public abstract string RadioZcciAzahar { get; }
    public abstract string RadioZipPerTitleId { get; }

    // Region
    public abstract string RegionChina { get; }
    public abstract string RegionEurope { get; }
    public abstract string RegionEuropePal { get; }
    public abstract string RegionFrance { get; }
    public abstract string RegionGermany { get; }
    public abstract string RegionItaly { get; }
    public abstract string RegionJapan { get; }
    public abstract string RegionKorea { get; }
    public abstract string RegionNetherlands { get; }
    public abstract string RegionNorthAmerica { get; }
    public abstract string RegionRussia { get; }
    public abstract string RegionSpain { get; }
    public abstract string RegionTaiwan { get; }
    public abstract string RegionWorld { get; }

    // Rename
    public abstract string RenameStatusDuplicate { get; }
    public abstract string RenameStatusEmptyName { get; }
    public abstract string RenameStatusMissingInfo { get; }
    public abstract string RenameStatusNameTooLong { get; }
    public abstract string RenameStatusReady { get; }
    public abstract string RenameStatusTargetExists { get; }
    public abstract string RenameStatusUnchanged { get; }
    public abstract string RenameSummary(object? arg0, object? arg1);
    public abstract string RenameSummarySkipped(object? arg0, object? arg1, object? arg2);

    // Result
    public abstract string ResultCompressSuccess { get; }
    public abstract string ResultConvertSuccessMultiTrack(object? arg0);
    public abstract string ResultExtractSuccessIso { get; }
    public abstract string ResultExtractSuccessTracks(object? arg0, object? arg1);

    // Status
    public abstract string StatusAnalyzing { get; }
    public abstract string StatusApplyingPatch { get; }
    public abstract string StatusApplyingPatchAndCompressing { get; }
    public abstract string StatusCancelled { get; }
    public abstract string StatusConverting { get; }
    public abstract string StatusConvertingToAt3 { get; }
    public abstract string StatusDecrypting { get; }
    public abstract string StatusDone { get; }
    public abstract string StatusDownloading { get; }
    public abstract string StatusDownloadingImage { get; }
    public abstract string StatusExtracting { get; }
    public abstract string StatusExtractingCia { get; }
    public abstract string StatusExtractingTitle(object? arg0, object? arg1);
    public abstract string StatusFailed { get; }
    public abstract string StatusHashCancelledByPatch { get; }
    public abstract string StatusHashFailed { get; }
    public abstract string StatusInitializing { get; }
    public abstract string StatusInstallDone(object? arg0);
    public abstract string StatusInstallStarted(object? arg0);
    public abstract string StatusInstalling { get; }
    public abstract string StatusLoading { get; }
    public abstract string StatusLoadingMetadata(object? arg0);
    public abstract string StatusLoadingSource { get; }
    public abstract string StatusMatched { get; }
    public abstract string StatusNoKey { get; }
    public abstract string StatusPresetLoaded { get; }
    public abstract string StatusRecognizing { get; }
    public abstract string StatusRecoveringTitleDb { get; }
    public abstract string StatusRecoveryDone(object? arg0);
    public abstract string StatusScanningTitles { get; }
    public abstract string StatusSelectSdAndMovable { get; }
    public abstract string StatusSkipped { get; }
    public abstract string StatusTitlesLoaded(object? arg0);
    public abstract string StatusUnmatched { get; }
    public abstract string StatusUnpacking { get; }
    public abstract string StatusUnsupported { get; }
    public abstract string StatusUnzipDone { get; }
    public abstract string StatusUnzipFailed { get; }
    public abstract string StatusUnzipping { get; }
    public abstract string StatusWaiting { get; }
    public abstract string StatusWrongPassword { get; }

    // Switch
    public abstract string SwitchLanguageAmericanEnglish { get; }
    public abstract string SwitchLanguageBrazilianPortuguese { get; }
    public abstract string SwitchLanguageBritishEnglish { get; }
    public abstract string SwitchLanguageCanadianFrench { get; }
    public abstract string SwitchLanguageDutch { get; }
    public abstract string SwitchLanguageFrench { get; }
    public abstract string SwitchLanguageGerman { get; }
    public abstract string SwitchLanguageItalian { get; }
    public abstract string SwitchLanguageJapanese { get; }
    public abstract string SwitchLanguageKorean { get; }
    public abstract string SwitchLanguageLatinAmericanSpanish { get; }
    public abstract string SwitchLanguagePolish { get; }
    public abstract string SwitchLanguagePortuguese { get; }
    public abstract string SwitchLanguageRussian { get; }
    public abstract string SwitchLanguageSimplifiedChinese { get; }
    public abstract string SwitchLanguageSpanish { get; }
    public abstract string SwitchLanguageThai { get; }
    public abstract string SwitchLanguageTraditionalChinese { get; }

    // Tab
    public abstract string TabArcade { get; }
    public abstract string TabCalculateHash { get; }
    public abstract string TabConvert { get; }
    public abstract string TabCreateCue { get; }
    public abstract string TabDecrypt { get; }
    public abstract string TabExtractCertsBin { get; }
    public abstract string TabFormatConvert { get; }
    public abstract string TabGeneral { get; }
    public abstract string TabIgnoreVersion { get; }
    public abstract string TabInstall { get; }
    public abstract string TabInstallNewGame { get; }
    public abstract string TabInstalledGames { get; }
    public abstract string TabKeyFile { get; }
    public abstract string TabLz4Compression { get; }
    public abstract string TabMerge { get; }
    public abstract string TabPatch { get; }
    public abstract string TabPbpPacking { get; }
    public abstract string TabPbpUnpack { get; }
    public abstract string TabRename { get; }
    public abstract string TabRepack { get; }
    public abstract string TabSaturnNspConvert { get; }
    public abstract string TabSettings { get; }
    public abstract string TabSplit { get; }
    public abstract string TabTistoryDownloader { get; }
    public abstract string TabUtil { get; }

    // Title
    public abstract string TitleArchivePassword { get; }
    public abstract string TitleCompressSettings { get; }
    public abstract string TitlePspSettings { get; }
    public abstract string TitleTypeSystem { get; }

    // Toggle
    public abstract string ToggleAlwaysOnTop { get; }
    public abstract string ToggleAutoCompressAfterPatch { get; }
    public abstract string ToggleAutoExtractDelete { get; }
    public abstract string ToggleAutoVersionDateInName { get; }
    public abstract string ToggleChangeOutputFolder { get; }
    public abstract string ToggleCheckUpdateOnStart { get; }
    public abstract string ToggleMergePatchToGame { get; }
    public abstract string ToggleOtherFormats { get; }
    public abstract string ToggleSoundEffects { get; }

    // ToolTip
    public abstract string ToolTipAddFile { get; }
    public abstract string ToolTipAddFolder { get; }
    public abstract string ToolTipAddSequentialPatch { get; }
    public abstract string ToolTipAutoExtractDelete { get; }
    public abstract string ToolTipBatchKoreanPatch { get; }
    public abstract string ToolTipBlockMode { get; }
    public abstract string ToolTipBlocklessMode { get; }
    public abstract string ToolTipBrowseOutput { get; }
    public abstract string ToolTipChangePatchFile { get; }
    public abstract string ToolTipChangePatchPackage { get; }
    public abstract string ToolTipClear { get; }
    public abstract string ToolTipCopyHash { get; }
    public abstract string ToolTipDragFolderToChangeDownload { get; }
    public abstract string ToolTipExpandCollapse { get; }
    public abstract string ToolTipForceLanguage { get; }
    public abstract string ToolTipForcedLanguage { get; }
    public abstract string ToolTipGameNameMax63 { get; }
    public abstract string ToolTipLicenseKey { get; }
    public abstract string ToolTipLicenseKeyPatchFallback { get; }
    public abstract string ToolTipMergeAll { get; }
    public abstract string ToolTipMergeGroupByTitleId { get; }
    public abstract string ToolTipMovableSedGuide { get; }
    public abstract string ToolTipOpenGithubReleases { get; }
    public abstract string ToolTipOpenHelp { get; }
    public abstract string ToolTipOpenWorkSpace { get; }
    public abstract string ToolTipPlayPause { get; }
    public abstract string ToolTipPublisherMax63 { get; }
    public abstract string ToolTipRecoverTitleDb { get; }
    public abstract string ToolTipRemoveSelected { get; }
    public abstract string ToolTipRestoreDefaultPath { get; }
    public abstract string ToolTipRomFsLz4 { get; }
    public abstract string ToolTipSelectAllToggle { get; }
    public abstract string ToolTipSelectFile { get; }
    public abstract string ToolTipTistoryUrlInput { get; }
    public abstract string ToolTipTitleIdInput { get; }
    public abstract string ToolTipValidateCompress { get; }

    // Update
    public abstract string LatestVersion { get; }
    public abstract string NoUpdateNotes { get; }
    public abstract string UpdateAvailable { get; }
    public abstract string UpdateDescription { get; }
    public abstract string UpdateTitle { get; }

    // Warn
    public abstract string WarnDuplicateDiscPathUsingLast(object? arg0);
    public abstract string WarnFileEntryMissingDiscOrExternal { get; }
    public abstract string WarnFolderEntryMissingDiscOrExternal { get; }
    public abstract string WarnIdRegionNotChecked { get; }
    public abstract string WarnOptionChoiceOutOfRange(object? arg0, object? arg1);
    public abstract string WarnOptionReferencesMissingPatch(object? arg0, object? arg1);
    public abstract string WarnPartialOverwriteUnsupported(object? arg0);
    public abstract string WarnUnsupportedElementCount(object? arg0, object? arg1);

    // Work
    public abstract string WorkTypeConvert { get; }
    public abstract string WorkTypeDecompress { get; }
    public abstract string WorkTypeRecompress { get; }

    public string GetItemStatus(ItemStatus status) => status switch
    {
        ItemStatus.Waiting => StatusWaiting,
        ItemStatus.Converting => StatusConverting,
        ItemStatus.Decrypting => StatusDecrypting,
        ItemStatus.Extracting => StatusExtracting,
        ItemStatus.Downloading => StatusDownloading,
        ItemStatus.Unzipping => StatusUnzipping,
        ItemStatus.UnzipDone => StatusUnzipDone,
        ItemStatus.UnzipFailed => StatusUnzipFailed,
        ItemStatus.Done => StatusDone,
        ItemStatus.Failed => StatusFailed,
        ItemStatus.Cancelled => StatusCancelled,
        ItemStatus.Unsupported => StatusUnsupported,
        ItemStatus.Skipped => StatusSkipped,
        _ => string.Empty
    };
}