using RomForge.Core.Models.Naming;

namespace RomForge.Core.Services.Naming;

public static class NamingTokenCatalog
{
    private static readonly NamingTokenOption[] LanguageOptions = [
        new("code1", "1자리", "1"),
        new("code2", "2자리", "2"),
        new("code3", "3자리", "3"),
        new(null, "전체 이름", string.Empty)
    ];

    private static readonly NamingTokenOption[] DateOptions = [
        new("yyMMdd", "6자리 (yyMMdd)", "6"),
        new("yyyyMMdd", "8자리 (yyyyMMdd)", "8")
    ];

    public static IReadOnlyList<NamingTokenDefinition> Patch { get; } = [
        new("fileName", "파일명", "원본 파일명 (언어 표기 제거)"),
        new("Lan1", "원본언어", "원본 파일명에서 찾은 언어", LanguageOptions, ["Language"]),
        new("Lan2", "패치언어", "패치 파일의 언어", LanguageOptions),
        new("Version", "버전", "패치 파일명에서 읽은 버전"),
        new("Date", "날짜", "패치 파일명에서 읽은 날짜", DateOptions)
    ];

    public static IReadOnlyList<NamingTokenDefinition> Rename { get; } = [
        new("Name", "한글+영어 파일명", "한글 + 영어 제목 (병합 파일명과 동일)"),
        new("KTitleName", "한글파일명", "한글 제목"),
        new("TitleName", "파일명", "한글 → 영어 → 첫 번째 제목 순"),
        new("TitleID", "타이틀ID", "타이틀 ID"),
        new("Tag", "구성", "구성 (B+U+10D)"),
        new("DisplayVersion", "표시버전", "표시 버전 (1.0.0)"),
        new("Version", Common.Localization.Loc.Instance.Current.HeaderVersion, "버전 (65536)")
    ];
}