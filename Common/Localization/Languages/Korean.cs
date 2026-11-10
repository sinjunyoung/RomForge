namespace Common.Localization.Languages;

public sealed class Korean : LocalizedStrings
{
    // Language
    public override string Code => "ko-KR";
    public override string DisplayName => "한국어";
    public override string EnglishName => "Korean";

    // Button
    public override string ButtonAddFile => "파일 추가";
    public override string ButtonAddFolder => "폴더 추가";
    public override string ButtonAnalyze => "분석";
    public override string ButtonCalculateHash => "해시 계산";
    public override string ButtonCancel => "취소";
    public override string ButtonCancelForceLanguage => "강제 언어 취소";
    public override string ButtonChange => "변경";
    public override string ButtonClear => "비우기";
    public override string ButtonDownload => "다운로드";
    public override string ButtonFilterAll => "전체";
    public override string ButtonFilterGame => "본편";
    public override string ButtonFilterUpdate => "업데이트";
    public override string ButtonForceLanguage => "강제 언어 설정";
    public override string ButtonInstall => "설치";
    public override string ButtonLater => "나중에";
    public override string ButtonLoadGameList => "게임목록 불러오기";
    public override string ButtonOk => "확인";
    public override string ButtonRecoverTitleDb => "title.db 복구";
    public override string ButtonRemoveSelected => "선택 삭제";
    public override string ButtonRepackFolderBased => "리팩 (폴더 기반)";
    public override string ButtonReset => "초기화";
    public override string ButtonSelectFile => "파일 선택";
    public override string ButtonSettings => "설정";
    public override string ButtonStart => "시작";
    public override string ButtonUnpack => "언팩";
    public override string ButtonUnpackAndRepackFull => "언팩 + 리팩 (Full)";
    public override string ButtonUnpackAndRepackStreaming => "언팩 + 리팩 (스트리밍 기반)";

    // Check
    public override string CheckExtraOption => "추가 옵션 (cmd 0x0B)";
    public override string CheckVideoFreezeFix => "동영상 프리징 픽스 (cmd 0x00)";

    // Description
    public override string DescArchivePasswordProtected => "이 압축파일은 비밀번호로 보호되어 있습니다.";
    public override string DescAutoCompressByRule => "하기 기준에 따라 포맷이 자동 선택되고 압축 또는 해제됩니다";
    public override string DescAutoMatchByExtension => "확장자 기준으로 자동 매칭됩니다";
    public override string DescAutoVersionDateInName => "패치 파일명에서 버전과 날짜를 읽어와 결과 파일명에 표시합니다";
    public override string DescCalculateFileHash => "파일 해시를 계산합니다";
    public override string DescCertsBinUsedForCia => "certs.bin 파일은 CIA 파일 생성 시에 사용됩니다";
    public override string DescClickOrDropToChange => "클릭하거나 파일을 드롭하여 변경할 수 있습니다.";
    public override string DescConvertByRule => "하기 기준으로 압축, 해제 및 포맷 변환을 진행합니다";
    public override string DescConvertByRuleMutual => "하기 기준으로 상호 변환합니다";
    public override string DescConvertFormats => "하기 파일 포맷을 상호 변환합니다";
    public override string DescCreateSingleTrackCue => "단일 트랙 CUE 파일을 생성합니다, 멀티 트랙 X";
    public override string DescDecryptFiles => "파일을 복호화합니다";
    public override string DescExtractCertsBin => "실행파일 위치에 certs.bin을 추출합니다";
    public override string DescInstallToConsole => "3DS 실기에 게임을 설치합니다";
    public override string DescMp3WavAutoConvertAt3 => "MP3, WAV 파일 추가 시 자동으로 AT3 포맷으로 변환됩니다.";
    public override string DescOutputFolderDefault => "해제 시 원본 ROM 폴더 안의 output 폴더에 저장";
    public override string DescPackMax5 => "최대 5개의 파일까지 패킹 가능";
    public override string DescPc98Hdi => "HDI (FAT12/16) 내부 파일을 논리 경로로 찾아 교체합니다. 마운트 경로는 자동으로 감지됩니다.";
    public override string DescRenameByFormat => "선택한 포맷에 맞춰 파일명을 일괄 변경합니다";
    public override string DescRestorePbpToBinCue => "PBP를 원본 BIN+CUE로 복원합니다";
    public override string DescSelectSdLoadList => "SD 카드를 선택하고 설치 목록을 불러오세요";
    public override string DescTistoryEnterUrl => "티스토리 URL을 입력하고 분석 버튼을 누르세요";
    public override string DescTistoryFindAttachments => "첨부파일 링크를 찾아 목록에 추가합니다";
    public override string DescVitaAutoClassify => "App/Patch/AddCont는 자동으로 분류됩니다";
    public override string DescVitaGroupToNoNpDrm => "같은 타이틀 ID의 App/Patch/AddCont는 하나로 묶여 NoNpDrm 포맷으로 변환됩니다";
    public override string DescVitaKeyRequiredShared => "키 (work.bin, *.rif/zRIF 문자열)가 필요합니다 (Patch는 App의 키를 공유할 수 있습니다)";
    public override string DescVitaPatchMerge => "에뮬레이터: 복호화 후 app/addcont 덮어씌우기 / 실기: rePatch/reAddcont 추가";
    public override string DescVitaPkgKeyRequired => "pkg의 경우 키 (work.bin, *.rif/zRIF 문자열)가 필요합니다";
    public override string DescWiiUAutoClassify => "본편/업데이트/DLC는 타이틀 ID로 자동 분류됩니다";

    // Dialog
    public override string DialogSelectGameFile => "게임 파일 선택";

    // Error
    public override string ErrorNoMetadata => "분석 가능한 메타데이터가 없습니다.";

    // Filter
    public override string FilterAllFiles => "모든 파일";
    public override string FilterSwitchFiles => "Switch 파일";

    // Header
    public override string HeaderCompressionRatio => "압축률";
    public override string HeaderConvert => "변환";
    public override string HeaderFileName => "파일명";
    public override string HeaderFileNameCompare => "파일명 비교 (현재/변경)";
    public override string HeaderFilePath => "경로";
    public override string HeaderForcedLanguage => "언어 고정";
    public override string HeaderFormat => "포맷";
    public override string HeaderInput => "입력";
    public override string HeaderKey => "키";
    public override string HeaderLocalTitle => "로컬 타이틀";
    public override string HeaderOutput => "출력";
    public override string HeaderPatch => "패치";
    public override string HeaderProductCode => "제품 코드";
    public override string HeaderResult => "결과";
    public override string HeaderSize => "용량";
    public override string HeaderTitle => "타이틀";
    public override string HeaderVersion => "버전";

    // Hint
    public override string HintDragOrClickPatch3ds => "패치 폴더 또는 zip/7z 파일을 드래그하거나 클릭하세요";
    public override string HintDragOrClickRiivolutionPatch => "Riivolution 패치 폴더 또는 zip/7z/xml을 드래그하거나 클릭하세요";
    public override string HintDragOrClickRom3ds => "롬 파일 (.3ds, .cci, .zcci, .cia)을 드래그하거나 클릭하세요";
    public override string HintDragOrClickWiiDisc => "Wii 디스크 (.iso, .wbfs, .wia, .rvz)를 드래그하거나 클릭하세요";
    public override string HintDragOrSelectOutputFolder => "출력 폴더를 드래그하거나 선택하세요";
    public override string HintDragOrSelectWorkFolder => "작업 폴더를 드래그하거나 선택하세요";
    public override string HintDropArcadeFiles => "원본 ZIP, 패치 (IPS, 폴더, ZIP)를 위에 드래그&드롭하세요";
    public override string HintDropCueChd => "CUE/CHD 파일을 드래그&드롭하세요";
    public override string HintDropFile => "파일을 여기에 드래그&드롭하세요";
    public override string HintDropFileOrFolder => "파일 또는 폴더를 여기에 드래그&드롭하세요";
    public override string HintDropGameFileOrFolder => "게임 파일/폴더를 여기에 드래그&드롭하세요";
    public override string HintDropImageFile => "이미지 파일을 드래그&드롭하세요";
    public override string HintDropOrClickAudio => "오디오 파일을 드래그&드롭하거나 클릭하세요";
    public override string HintDropPatchFile => "패치 파일을 드래그&드롭하세요";
    public override string HintDropSourceNsp => "원본 NSP/NSZ 파일을 드래그&드롭하세요";
    public override string HintDropVitaFiles => "PS Vita 파일 (pkg, zip)/폴더를 여기에 드래그&드롭하세요";
    public override string HintDropVitaPkg => "PS Vita PKG 파일/폴더를 여기에 드래그&드롭하세요";
    public override string HintDropWiiUFiles => "WUP/Loadiine 폴더, wud/wux/wua 파일을 여기에 드래그&드롭하세요";
    public override string HintRepackReplaceRule => "본편/업데이트는 자동 교체, DLC는 계속 추가됩니다";
    public override string HintSearchGame => "게임 검색...";
    public override string HintSelectOutput => "출력 폴더를 선택하세요";
    public override string HintSoundCancel => "취소 (.mp3, .wav 파일을 드래그&드롭하세요)";
    public override string HintSoundComplete => "완료 (.mp3, .wav 파일을 드래그&드롭하세요)";
    public override string HintSoundError => "에러 (.mp3, .wav 파일을 드래그&드롭하세요)";
    public override string HintSplitExpandRule => "통합롬은 + 버튼으로 펼쳐서 본편/업데이트/DLC를 골라 분리할 수 있습니다";

    // Label
    public override string LabelActual => "실제";
    public override string LabelAlgorithm => "알고리즘:";
    public override string LabelAllFormats => "모든 포맷";
    public override string LabelBackgroundMusic => "배경음";
    public override string LabelBootLogo => "부트 로고";
    public override string LabelCompression => "압축";
    public override string LabelCompressionAlgorithmIso => "압축 알고리즘 (ISO: PSP, PS2)";
    public override string LabelCompressionLevel => "압축 레벨";
    public override string LabelCompressionLevelNszXcz => "압축 레벨 (NSZ, XCZ)";
    public override string LabelCompressionMode => "압축 모드";
    public override string LabelDefaultImage => "기본 이미지";
    public override string LabelDownloadLocation => "다운로드 위치";
    public override string LabelEstimated => "예상";
    public override string LabelExpectedFileName => "예상 파일명:";
    public override string LabelFile => "파일";
    public override string LabelFileCount(object arg0) => $"파일 {arg0}개";
    public override string LabelFormat3dsCci => "3DS (CCI와 같은 포맷)";
    public override string LabelFormatBinCueSingleTrack => "BIN+CUE (단일 트랙)";
    public override string LabelFormatChdIsoBased => "CHD (ISO 기반)";
    public override string LabelFormatList3dsCciCia => "3DS (CCI와 같은 포맷), CCI, CIA";
    public override string LabelFormatListIsoStandard => "ISO (표준), CUE+BIN, GDI+BIN+RAW, CCD+IMG+SUB";
    public override string LabelFormatListLoadiineFolderWua => "Loadiine 폴더, WUA";
    public override string LabelFormatListWuaWupLoadiine => "WUA, WUP 폴더, Loadiine 폴더";
    public override string LabelFormatListWupFolderWua => "WUP 폴더, WUA";
    public override string LabelFormatListWupLoadiine => "WUP 폴더, Loadiine 폴더";
    public override string LabelGameId => "게임 ID";
    public override string LabelGameName => "게임명";
    public override string LabelGeneral => "일반";
    public override string LabelLanguage => "언어";
    public override string LabelLz4Compression => "Lz4 압축";
    public override string LabelMergeMode => "병합 방식:";
    public override string LabelMultiRomIndex => "멀티롬 인덱스";
    public override string LabelNoteCemuArchive => "(Cemu 아카이브)";
    public override string LabelNoteDiscImage => "(디스크 이미지)";
    public override string LabelNoteFolder => "(폴더)";
    public override string LabelNspTitleId => "NSP 타이틀 ID";
    public override string LabelOptions => "옵션:";
    public override string LabelOthers => "이외 기타";
    public override string LabelOutputFormat => "출력 포맷:";
    public override string LabelOutputOptions => "출력 옵션";
    public override string LabelPatch => "패치";
    public override string LabelPatchInfo => "패치 정보";
    public override string LabelPbpPacking => "PBP 패킹";
    public override string LabelPkgCount(object arg0) => $"PKG {arg0}개";
    public override string LabelPopsEmulatorSettings => "POPS 에뮬레이터 설정";
    public override string LabelPublisher => "배급사";
    public override string LabelQuality => "품질";
    public override string LabelSelectPatch => "패치 선택:";
    public override string LabelSettings => "설정";
    public override string LabelSoundVolume => "사운드 볼륨:";
    public override string LabelSource => "원본";
    public override string LabelSourceAutoExtractFormats => "원본 자동 압축 해제 지원 포맷:";
    public override string LabelSupportedLanguages => "지원 언어";
    public override string LabelSupportedPatchFormats => "지원 패치 포맷:";
    public override string LabelTistoryUrl => "티스토리 URL";
    public override string LabelVersion => "버전";
    public override string LabelWideScreen => "와이드 스크린";

    // Log
    public override string LogAnalyzeMetadata => "━━ 메타데이터 통합 분석 중... ━━";
    public override string LogCompressAndMerge => "압축하며 병합 중...";
    public override string LogCompressAndSplit => "압축하며 분리 중...";
    public override string LogCompressionRatio => "압축률";
    public override string LogDecompressAndMerge => "압축 해제 및 병합 중";
    public override string LogDeleteIncompleteFile => "불완전하게 생성된 파일을 삭제했습니다.";
    public override string LogError(object arg0) => $"에러 발생: {arg0}";
    public override string LogFinalId(object arg0, object arg1) => $"최종 ID: {arg0} / 버전: {arg1}";
    public override string LogMergeComplete(object arg0) => $"병합 완료: {arg0}";
    public override string LogMergeFailed(object? arg0, object arg1) => $"[{arg0}] 병합 실패: {arg1}";
    public override string LogMerging => "병합 중...";
    public override string LogModeCompress => "압축";
    public override string LogModeDecompress => "해제";
    public override string LogProcessError => "처리 오류";
    public override string LogProcessStart => "시작";
    public override string LogSplitComplete(object arg0) => $"분리 완료: {arg0}";
    public override string LogSplitFailed(object arg0, object arg1) => $"분리 실패 (ID: {arg0}): {arg1}";
    public override string LogSplitPreparing(object arg0) => $"{arg0} 분리 시작";
    public override string LogSplitting => "분리 중...";
    public override string LogStatusCompressing => "압축 중...";
    public override string LogStatusCopying => "복사 중...";
    public override string LogStatusDecompressing => "해제 중...";
    public override string LogStatusDone => "완료";
    public override string LogStatusValidating => "검증 중...";
    public override string LogTitleGroupDetected(object arg0) => $"{arg0}개 타이틀 그룹 감지됨";
    public override string LogValidationComplete => "검증 완료";
    public override string LogValidationStart => "검증 시작";

    // Main
    public override string MainErrNoFiles => "파일을 추가한 후 다시 시도하세요.";
    public override string MainErrNoKeys => "prod.keys 파일이 없습니다. 키 파일을 실행 폴더에 넣어주세요.";
    public override string MainErrNoOutput => "출력 폴더를 설정하세요.";
    public override string MainLogAllComplete(object arg0) => $"✓ 모든 작업 완료  총 소요: {arg0}";
    public override string MainLogRecalculating => "파일 타입 재분석 중...";
    public override string MainMsgDone => "작업 완료!";
    public override string MainMsgSplitDone => "분리가 완료되었습니다.";

    // Menu
    public override string MenuClear => "비우기";
    public override string MenuClearLog => "로그 비우기";
    public override string MenuCopyLog => "로그 복사";
    public override string MenuDeselect => "선택 취소";
    public override string MenuExtractCci => "CCI로 추출";
    public override string MenuExtractCia => "CIA로 추출";
    public override string MenuFindInArchive => "압축파일에서 찾기 (zip/7z)...";
    public override string MenuFindInFolder => "폴더에서 찾기...";
    public override string MenuOpenFolder => "폴더 열기";
    public override string MenuRemoveSelected => "선택 삭제";
    public override string MenuSelectArchive => "압축파일 선택 (zip/7z)...";
    public override string MenuSelectFileIpsZip => "파일 선택 (*.ips, *.zip)...";
    public override string MenuSelectFileZip7z => "파일 선택 (*.zip, *.7z)...";
    public override string MenuSelectFileZip7zXml => "파일 선택 (*.zip, *.7z, *.xml)...";
    public override string MenuSelectFolder => "폴더 선택...";

    // Message
    public override string MsgTitleError => "오류";
    public override string MsgTitleInfo => "알림";
    public override string MsgTitleQuestion => "확인";
    public override string MsgTitleWarning => "경고";

    // Option
    public override string OptionLevel0None => "0: 무압축";
    public override string OptionLevel18Recommended => "18: 권장";
    public override string OptionLevel3Fastest => "3: 최대 속도";
    public override string OptionLevel5Balanced => "5: 균형";
    public override string OptionLevel9Balanced => "9: 균형";
    public override string OptionLevel9Max => "9: 최대";

    // Radio
    public override string RadioBlock => "블록";
    public override string RadioBlockless => "블록리스";
    public override string RadioCciEmulator => "CCI (에뮬레이터)";
    public override string RadioCiaConsole => "CIA (실기)";
    public override string RadioConsoleRepatch => "실기 (rePatch, reAddcont)";
    public override string RadioEmulatorAppAddcont => "에뮬레이터 (app, addcont)";
    public override string RadioFolderPerTitleId => "폴더 (타이틀 ID별)";
    public override string RadioLowercase => "소문자";
    public override string RadioMergeAll => "전체";
    public override string RadioMergeGroupByTitleId => "타이틀 ID별 그룹핑 (기존 방식)";
    public override string RadioOutputGameId => "실기 (타이틀 ID/EBOOT.PBP)";
    public override string RadioOutputNormal(object arg0) => $"일반 (타이틀명.{arg0})";
    public override string RadioQuality30s => "30초 (132k)";
    public override string RadioQuality60s => "60초 (66k)";
    public override string RadioRvzDolphin => "RVZ (Dolphin 전용 압축)";
    public override string RadioUppercase => "대문자";
    public override string RadioWbfsUsbLoader => "WBFS (USB 로더)";
    public override string RadioWuaEmulator => "WUA (에뮬레이터)";
    public override string RadioWupConsole => "WUP (실기)";
    public override string RadioZcciAzahar => "ZCCI (Azahar 전용 압축)";
    public override string RadioZipPerTitleId => "ZIP (타이틀 ID별)";

    // Status
    public override string StatusAnalyzing => "분석 중...";
    public override string StatusConvertingToAt3 => "AT3로 변환 중...";
    public override string StatusDownloadingImage => "이미지 다운로드 중...";
    public override string StatusHashCancelledByPatch => "패치 시작으로 해시 계산이 취소되었습니다";
    public override string StatusHashFailed => "해시 계산 실패";
    public override string StatusLoading => "로딩 중...";
    public override string StatusMatched => "매칭";
    public override string StatusNoKey => "키 없음";
    public override string StatusPresetLoaded => "프리셋 설정 로드됨 (수동 설정 비활성화)";
    public override string StatusUnmatched => "미매칭";
    public override string StatusWrongPassword => "비밀번호가 올바르지 않습니다.";

    // Tab
    public override string TabArcade => "아케이드";
    public override string TabCalculateHash => "해시 계산";
    public override string TabConvert => "변환";
    public override string TabCreateCue => "Cue 생성";
    public override string TabDecrypt => "복호화";
    public override string TabExtractCertsBin => "certs.bin 추출";
    public override string TabFormatConvert => "포맷 변환";
    public override string TabGeneral => "일반";
    public override string TabIgnoreVersion => "버전 무시";
    public override string TabInstall => "설치";
    public override string TabInstalledGames => "설치된 게임목록";
    public override string TabInstallNewGame => "신규 게임 설치";
    public override string TabKeyFile => "키 파일";
    public override string TabLz4Compression => "Lz4 압축";
    public override string TabMerge => "병합";
    public override string TabPatch => "패치";
    public override string TabPbpPacking => "PBP 패킹";
    public override string TabPbpUnpack => "PBP 언팩";
    public override string TabRename => "파일명 변경";
    public override string TabRepack => "리팩";
    public override string TabSaturnNspConvert => "새턴 NSP 변환";
    public override string TabSettings => "설정";
    public override string TabSplit => "분리";
    public override string TabTistoryDownloader => "티스토리 다운로더";
    public override string TabUtil => "유틸";

    // Title
    public override string TitleArchivePassword => "압축파일 비밀번호";

    // Toggle
    public override string ToggleAlwaysOnTop => "항상 다른 창 위에 표시";
    public override string ToggleAutoCompressAfterPatch => "패치 후 자동 압축";
    public override string ToggleAutoExtractDelete => "압축 자동 해제 (완료 후 원본 삭제)";
    public override string ToggleAutoVersionDateInName => "결과 파일명에 버전/날짜 자동 표기";
    public override string ToggleChangeOutputFolder => "출력 폴더 변경";
    public override string ToggleCheckUpdateOnStart => "앱 구동 시 업데이트 확인";
    public override string ToggleMergePatchToGame => "게임에 패치 병합";
    public override string ToggleOtherFormats => "이외 기타";
    public override string ToggleSoundEffects => "효과음";

    // ToolTip
    public override string ToolTipAddFile => "파일 추가";
    public override string ToolTipAddFolder => "폴더 추가";
    public override string ToolTipAddSequentialPatch => "연속으로 적용할 패치 추가";
    public override string ToolTipAutoExtractDelete => "다운로드한 파일이 압축파일 (zip/7z/rar)이면 자동으로 압축을 풀고 원본 압축파일을 삭제합니다";
    public override string ToolTipBatchKoreanPatch => "한글패치 일괄 지정 (루트 폴더 또는 압축파일)";
    public override string ToolTipBlocklessMode => "순차 액세스 (용량 우선)";
    public override string ToolTipBlockMode => "랜덤 액세스 (속도 우선)";
    public override string ToolTipBrowseOutput => "출력 폴더 선택";
    public override string ToolTipChangePatchFile => "다른 패치 파일로 변경";
    public override string ToolTipChangePatchPackage => "다른 패치 패키지로 변경";
    public override string ToolTipClear => "비우기";
    public override string ToolTipCopyHash => "해시값 복사";
    public override string ToolTipDragFolderToChangeDownload => "폴더를 이곳에 드래그하여 다운로드 위치를 변경할 수 있습니다.";
    public override string ToolTipExpandCollapse => "펼치기/접기";
    public override string ToolTipForcedLanguage => "설치 시 이 언어로 로케일을 고정합니다 (locale.txt 자동 생성). SMDH에 실제로 존재하는 언어만 표시됩니다.";
    public override string ToolTipForceLanguage => "리팩 진행 시 선택한 언어만 플래그를 켜고 나머지는 전부 끕니다.";
    public override string ToolTipGameNameMax63 => "게임명 (최대 63자)";
    public override string ToolTipLicenseKey => "라이선스 파일 경로 (work.bin, *.rif)/zRIF 문자열";
    public override string ToolTipLicenseKeyPatchFallback => "라이선스 파일 경로 (work.bin, *.rif)/zRIF 문자열 (Patch는 비워두면 App의 키를 사용)";
    public override string ToolTipMergeAll => "전체 항목을 하나의 파일로 병합합니다.";
    public override string ToolTipMergeGroupByTitleId => "타이틀 ID별로 항목을 그룹화하여 각각 별도의 파일로 병합합니다.";
    public override string ToolTipMovableSedGuide => "1. GodMode9 실행 (START + POWER)\n2. SYSNAND CTRNAND → private → movable.sed 선택\n3. 가장 하단 Copy to 0:/gm9/out 실행";
    public override string ToolTipOpenGithubReleases => "깃허브 릴리즈 페이지를 엽니다";
    public override string ToolTipOpenHelp => "도움말 페이지를 엽니다";
    public override string ToolTipOpenWorkSpace => "출력 폴더 열기";
    public override string ToolTipPlayPause => "재생/일시정지";
    public override string ToolTipPublisherMax63 => "배급사 (최대 63자)";
    public override string ToolTipRecoverTitleDb => "3DS 실기에서 title.db 오류로 설치한 게임이 HOME 메뉴에 나타나지 않거나 인식되지 않을 때 사용합니다.\n\nSD 카드에 설치된 타이틀을 기준으로 title.db와 import.db를 새로 만듭니다.\n기존 title.db/import.db는 삭제되며, 게임 파일은 변경되지 않습니다.\n정상적으로 인식되고 있다면 누를 필요가 없습니다.";
    public override string ToolTipRemoveSelected => "선택 삭제";
    public override string ToolTipRestoreDefaultPath => "기본 위치로 되돌리기";
    public override string ToolTipRomFsLz4 => "RomFS Lz4 압축 (리팩 시 적용). 본편과 업데이트는 하나로 합쳐지므로 둘 중 하나라도 체크하면 압축됩니다.";
    public override string ToolTipSelectAllToggle => "전체 선택/해제";
    public override string ToolTipSelectFile => "파일 선택";
    public override string ToolTipTistoryUrlInput => "티스토리 게시글 URL을 한 줄에 하나씩 입력하세요 (여러 개 입력 가능, Ctrl+Enter로 분석 실행)";
    public override string ToolTipTitleIdInput => "16자리 16진수 (예: 0100000000010000). 비워두면 원본 타이틀 ID 유지.";
    public override string ToolTipValidateCompress => "압축 후 추가 유효성 검사를 실행합니다";

    // Update
    public override string LatestVersion => "최신 버전";
    public override string NoUpdateNotes => "업데이트 내용이 없습니다.";
    public override string UpdateAvailable => "새로운 업데이트";
    public override string UpdateDescription => "RomForge의 새로운 버전이 릴리즈 되었습니다.";
    public override string UpdateTitle => "RomForge 업데이트";
}