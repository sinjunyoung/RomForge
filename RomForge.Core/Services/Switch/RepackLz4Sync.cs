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
        ulong? baseTitleId = baseEntry != null && ulong.TryParse(baseEntry.TitleID, NumberStyles.HexNumber, null, out ulong parsedBaseId) ? parsedBaseId : null;

        foreach (var file in files.Where(f => f.CanSetLz4))
        {
            bool? unpackedLz4 = null;

            if (baseTitleId.HasValue)
            {
                if (IsDlcRow(file))
                {
                    if (ulong.TryParse(file.TitleID, NumberStyles.HexNumber, null, out ulong dlcTitleId))
                        unpackedLz4 = RomfsCompression.ReadUnpackedDlcFlag(unpackedDir, baseTitleId.Value, dlcTitleId);
                }
                else
                {
                    unpackedLz4 = RomfsCompression.ReadUnpackedFlag(unpackedDir, baseTitleId.Value);
                }
            }

            file.ApplyLz4Detection(file.SourceHasCompressedRomfs, unpackedLz4);
        }
    }

    public static bool? ResolveProgramOverride(IEnumerable<GameFile>? gameFiles, bool rebuildOnly)
    {
        if (gameFiles == null)
            return null;

        var programRows = gameFiles.Where(f => !IsDlcRow(f) && f.CanSetLz4).ToList();

        if (programRows.All(f => f.GetLz4Override(rebuildOnly) == null))
            return null;

        return programRows.Any(f => f.IsLz4);
    }

    public static IReadOnlyDictionary<string, bool> ResolveDlcOverrides(IEnumerable<GameFile>? gameFiles, bool rebuildOnly)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        if (gameFiles == null)
            return result;

        foreach (var file in gameFiles.Where(f => IsDlcRow(f) && !string.IsNullOrEmpty(f.TitleID)))
        {
            if (file.GetLz4Override(rebuildOnly) is bool value)
                result[file.TitleID!] = value;
        }

        return result;
    }

    private static bool IsDlcRow(GameFile file) => file.FileType.Contains('D') && !file.FileType.Contains('B') && !file.FileType.Contains('U');
}