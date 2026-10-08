using System.IO;
using RomForge.Core.Models._3DS;

namespace RomForge.ViewModels._3DS;

public sealed record ThreeDsPatchDisplay(string KindText, string Name, string TargetText, string FileCountText, string StatusText, bool IsError)
{
    public static ThreeDsPatchDisplay From(ThreeDsPatchSet patch, string? titleId, string? productCode)
    {
        string status;
        bool isError = false;

        if (string.IsNullOrEmpty(titleId) && string.IsNullOrEmpty(productCode))
            status = "원본 롬을 지정하면 일치 여부를 확인합니다.";
        else if (patch.MatchesRom(titleId, productCode))
            status = "원본 롬과 일치합니다.";
        else
        {
            status = "원본 롬과 패치 대상 정보가 맞지 않습니다.";
            isError = true;
        }

        string kindText = patch.Kind switch
        {
            ThreeDsPatchKind.Folder => "폴더",
            ThreeDsPatchKind.Zip => "ZIP",
            ThreeDsPatchKind.SevenZip => "7Z",
            _ => "기타"
        };

        string target = patch.TargetIds.Count == 0 ? "제한 없음" : string.Join(", ", patch.TargetIds);

        return new ThreeDsPatchDisplay(kindText, Path.GetFileName(patch.Path), $"대상 타이틀 ID: {target}", $"파일: {patch.FileCount:N0}개", status, isError);
    }

    public static ThreeDsPatchDisplay Failed(string message) => new("오류", "-", string.Empty, string.Empty, message, true);
}