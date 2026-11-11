using System.IO;
using Common.Localization;
using RomForge.Core.Models.N3DS;

namespace RomForge.Core.Services.N3DS;

public sealed record PatchDisplay(string KindText, string Name, string TargetText, string FileCountText, string StatusText, bool IsError)
{
    public static PatchDisplay From(PatchSet patch, string? titleId, string? productCode)
    {
        string status;
        bool isError = false;

        if (string.IsNullOrEmpty(titleId) && string.IsNullOrEmpty(productCode))
            status = Loc.S.MsgPatchSpecifyRomToVerify;
        else if (patch.MatchesRom(titleId, productCode))
            status = Loc.S.MsgPatchRomMatches;
        else
        {
            status = Loc.S.MsgPatchRomMismatch;
            isError = true;
        }

        string kindText = patch.Kind switch
        {
            PatchKind.Folder => Loc.S.LabelFolder,
            PatchKind.Zip => "ZIP",
            PatchKind.SevenZip => "7Z",
            _ => Loc.S.LabelOther
        };

        string target = patch.TargetIds.Count == 0 ? Loc.S.LabelUnlimited : string.Join(", ", patch.TargetIds);

        return new PatchDisplay(kindText, Path.GetFileName(patch.Path), Loc.S.LabelTargetTitleId(target), Loc.S.LabelFileCountN(patch.FileCount.ToString("N0")), status, isError);
    }

    public static PatchDisplay Failed(string message) => new(Loc.S.MsgTitleError, "-", string.Empty, string.Empty, message, true);
}