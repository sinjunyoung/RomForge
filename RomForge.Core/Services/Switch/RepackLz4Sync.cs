using NSW.Core.Services;
using RomForge.Core.ViewModels.Switch;
using System.Globalization;

namespace RomForge.Core.Services.Switch;

public static class RepackLz4Sync
{
    public static void Refresh(IEnumerable<GameFile> gameFiles, string unpackedDir)
    {
        var files = gameFiles.ToList();
        var baseEntry = files.FirstOrDefault(f => f.FileType.Contains('B'));

        if (baseEntry == null)
            return;

        var updateEntry = files.FirstOrDefault(f => f.FileType.Contains('U'));
        bool sourceLz4 = baseEntry.SourceHasCompressedRomfs || (updateEntry?.SourceHasCompressedRomfs ?? false) || files.Any(f => f.FileType.Contains('D') && f.SourceHasCompressedRomfs); bool? unpackedLz4 = null;

        if (ulong.TryParse(baseEntry.TitleID, NumberStyles.HexNumber, null, out ulong baseTitleId))
            unpackedLz4 = RomfsCompression.ReadUnpackedFlag(unpackedDir);

        baseEntry.ApplyLz4Detection(sourceLz4, unpackedLz4);
    }
}