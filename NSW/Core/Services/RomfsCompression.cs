using LibHac.Tools.FsSystem.NcaUtils;
using NSW.Core.Models;
using System.Globalization;
using System.IO;
using Path = System.IO.Path;

namespace NSW.Core.Services;

public static class RomfsCompression
{
    public const string FlagFileName = "romfs.compressed";

    public static bool HasCompressedSection(Nca? nca)
    {
        if (nca == null)
            return false;

        for (int i = 0; i < 4; i++)
            if (nca.SectionExists(i) && nca.GetFsHeader(i).ExistsCompressionLayer())
                return true;

        return false;
    }

    public static void Apply(UnpackResult result, bool compress)
    {
        foreach (var (idOffset, romfsDir) in result.RomfsDirs)
        {
            string? programDir = Path.GetDirectoryName(romfsDir);

            if (string.IsNullOrEmpty(programDir))
                continue;

            string flagPath = Path.Combine(programDir, FlagFileName);

            if (compress)
            {
                File.WriteAllText(flagPath, string.Empty);
                result.CompressedRomfs.Add(idOffset);
            }
            else
            {
                if (File.Exists(flagPath))
                    File.Delete(flagPath);

                result.CompressedRomfs.Remove(idOffset);
            }
        }
    }

    public static bool? ReadUnpackedFlag(string unpackedDir)
    {
        if (string.IsNullOrEmpty(unpackedDir) || !Directory.Exists(unpackedDir))
            return null;

        var programDirs = new List<(ulong Id, string Dir)>();

        foreach (var dir in Directory.GetDirectories(unpackedDir))
        {
            string name = Path.GetFileName(dir);

            if (name.Length == 16 && ulong.TryParse(name, NumberStyles.HexNumber, null, out ulong id))
                programDirs.Add((id, dir));
        }

        if (programDirs.Any(p => File.Exists(Path.Combine(p.Dir, FlagFileName))))
            return true;

        string dlcRoot = Path.Combine(unpackedDir, "DLCs");

        return Directory.Exists(dlcRoot) && Directory.GetDirectories(dlcRoot).Any(d => File.Exists(Path.Combine(d, FlagFileName)));
    }
}