namespace Common.Localization.Languages;

public sealed class Korean : LocalizedStrings
{
    public override string Code => "ko-KR";
    public override string DisplayName => "한국어";
    public override string EnglishName => "Korean";

    // Button
    public override string ButtonCancel => "취소";
    public override string ButtonSettings => "설정";
    public override string ButtonStart => "시작";

    // Dialog
    public override string DialogSelectGameFile => "게임 파일 선택";

    // Error
    public override string ErrorNoMetadata => "분석 가능한 메타데이터가 없습니다.";

    // Filter
    public override string FilterAllFiles => "모든 파일";
    public override string FilterSwitchFiles => "Switch 파일";

    // Hint
    public override string HintSelectOutput => "출력 폴더를 선택하세요";

    // Header
    public override string HeaderFileName => "파일명";
    public override string HeaderFilePath => "경로";
    public override string HeaderTitle => "타이틀";
    public override string HeaderPatch => "패치";
    public override string HeaderVersion => "버전";
    public override string HeaderInput => "입력";
    public override string HeaderOutput => "출력";

    // Label    
    public override string LabelFile => "파일";
    public override string LabelCompression => "압축";
    public override string LabelCompressionRatio => "압축률";
    public override string LabelOutputFormat => "출력 포맷:";

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
    public override string MainErrNoOutput => "작업 폴더를 설정하세요.";
    public override string MainLogAllComplete(object arg0) => $"✓ 모든 작업 완료  총 소요: {arg0}";
    public override string MainLogRecalculating => "파일 타입 재분석 중...";
    public override string MainMsgDone => "작업완료!";
    public override string MainMsgSplitDone => "분리가 완료되었습니다.";

    // Menu
    public override string MenuOpenFolder => "폴더 열기";

    // Msg
    public override string MsgTitleError => "오류";
    public override string MsgTitleInfo => "알림";
    public override string MsgTitleQuestion => "확인";
    public override string MsgTitleWarning => "경고";

    // Status
    public override string StatusAnalyzing => "분석 중...";
    public override string StatusNoKey => "키 없음";

    // ToolTip
    public override string ToolTipAddFile => "파일 추가";
    public override string ToolTipAddFolder => "폴더 추가";
    public override string ToolTipBlocklessMode => "순차 액세스 (용량 우선)";
    public override string ToolTipBlockMode => "랜덤 액세스 (속도 우선)";
    public override string ToolTipOpenWorkSpace => "출력 폴더 열기";
    public override string ToolTipRemoveAll => "비우기";
    public override string ToolTipRemoveSelected => "선택 삭제";
    public override string ToolTipValidateCompress => "압축 후 추가 유효성 검사를 실행합니다";
}