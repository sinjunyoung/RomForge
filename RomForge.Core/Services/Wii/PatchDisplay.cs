using RomForge.Core.Models.Wii;
using Common.Localization;
using System.IO;
using WiiGC.Core.Services.Wii;

namespace RomForge.Core.Services.Wii;

public sealed record PatchDisplay(string KindText, string XmlName, string TargetText, string FileCountText, string WarningText, string StatusText, bool IsError)
{
    public static PatchDisplay From(RiivolutionSourceKind kind, RiivolutionPatchSet patch, string? discGameId)
    {
        string status;
        bool isError = false;

        if (discGameId == null)
            status = Loc.S.MsgPatchSpecifyDiscToVerify;
        else if (patch.MatchesDisc(discGameId))
            status = Loc.S.MsgPatchDiscMatches;
        else
        {
            status = Loc.S.MsgPatchDiscGameIdMismatch;
            isError = true;
        }

        string kindText = kind switch
        {
            RiivolutionSourceKind.Folder => Loc.S.LabelFolder,
            RiivolutionSourceKind.Xml => "XML",
            RiivolutionSourceKind.Zip => "ZIP",
            _ => "7Z"
        };
        string target = patch.GameIds.Count == 0 ? Loc.S.LabelUnlimited : string.Join(", ", patch.GameIds);
        string warning = patch.Warnings.Count == 0 ? Loc.S.LabelNone : Loc.S.LabelWarningsCountShownInLog(patch.Warnings.Count);

        return new PatchDisplay(
            kindText,
            Path.GetFileName(patch.XmlPath),
            Loc.S.LabelTargetGameId(target),
            Loc.S.LabelReplacementFiles(patch.Replacements.Count.ToString("N0")),
            Loc.S.LabelWarningValue(warning),
            status,
            isError);
    }

    public static PatchDisplay Failed(string message) => new(Loc.S.MsgTitleError, "-", string.Empty, string.Empty, string.Empty, message, true);
}