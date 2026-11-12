using CHD.Core.Interop;
using Common.Localization;
using CHD.Core.Interop.Enums;
using CHD.Core.Models;
using CHD.Core.Services;
using NSW.Core.Services;
using PSP.PBP.Models;
using PSP.PBP.Services;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Path = System.IO.Path;

namespace RomForge.Core.Services.Switch;

public static partial class SaturnRomfsInjector
{
    [GeneratedRegex("^\\s*FILE\\s+(?:\"(?<q>[^\"]+)\"|(?<u>\\S+))", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex FileLineRegex();

    private const int RawSectorSize = 2352;

    public static void Inject(VirtualRomfs romfs, string discPath, bool wideScreen, double volume)
    {
        string oldCue = romfs.EnumerateRelativeFiles().FirstOrDefault(p => p.EndsWith(".cue", StringComparison.OrdinalIgnoreCase)) ?? throw new FileNotFoundException(Loc.S.ErrCueNotFoundInRomfs);
        string dir = GetDirectory(oldCue);
        string cueFileName = oldCue[dir.Length..];
        string baseName = Path.GetFileNameWithoutExtension(cueFileName);

        RemoveOldDisc(romfs, oldCue, dir);

        if (string.Equals(Path.GetExtension(discPath), ".chd", StringComparison.OrdinalIgnoreCase))
            AddChd(romfs, discPath, dir, cueFileName, baseName);
        else
            AddCue(romfs, discPath, dir, cueFileName);

        WriteIni(romfs, dir, baseName, wideScreen, volume);
    }

    private static string GetDirectory(string relativePath)
    {
        int slash = relativePath.LastIndexOf('/');
        return slash < 0 ? string.Empty : relativePath[..(slash + 1)];
    }

    private static void RemoveOldDisc(VirtualRomfs romfs, string oldCue, string dir)
    {
        string cueText = Encoding.UTF8.GetString(romfs.ReadAllBytes(oldCue));

        foreach (Match match in FileLineRegex().Matches(cueText))
        {
            string name = (match.Groups["q"].Success ? match.Groups["q"].Value : match.Groups["u"].Value).Replace('\\', '/');

            romfs.Remove(dir + name);
        }

        romfs.Remove(oldCue);
    }

    private static void AddChd(VirtualRomfs romfs, string chdPath, string dir, string cueFileName, string baseName)
    {
        var info = ChdInfoReader.ReadChdInfo(chdPath);

        if (info.Tracks == null || info.TrackCount <= 0)
            throw new InvalidDataException(Loc.S.ErrChdTrackInfoUnreadable);

        long length = info.Tracks.Take(info.TrackCount).Sum(t => (long)t.Frames * RawSectorSize);
        string binName = baseName + ".bin";
        var cue = CueFileReader.BuildCueFromChdInfo(info);

        cue.Entries[0].FileName = binName;

        for (int i = 0; i < cue.Entries[0].Tracks.Count; i++)
            cue.Entries[0].Tracks[i].DataType = MapTrackType(info.Tracks[i].TrackType);

        romfs.WriteAllBytes(dir + cueFileName, Encoding.UTF8.GetBytes(BuildCueText(cue)));
        romfs.Add(dir + binName, length, () => OpenChd(chdPath, length, info));
    }

    private static string MapTrackType(string? chdTrackType)
    {
        string type = chdTrackType?.ToUpperInvariant() ?? string.Empty;

        if (type.Contains("AUDIO"))
            return "AUDIO";

        if (type.StartsWith("MODE1"))
            return "MODE1/2352";

        return "MODE2/2352";
    }

    private static ChdReadStream OpenChd(string chdPath, long length, ChdInfo info)
    {
        var wrapper = new LibChdrWrapper();
        var error = wrapper.Open(chdPath);

        if (error != ChdrError.CHDERR_NONE)
        {
            wrapper.Dispose();
            throw new IOException(Loc.S.ErrChdOpenFailed(LibChdrWrapper.GetErrorString(error)));
        }

        return new ChdReadStream(wrapper, length, info, true);
    }

    private static void AddCue(VirtualRomfs romfs, string cuePath, string dir, string cueFileName)
    {
        var bins = ConversionSource.ParseBinsFromCue(cuePath);

        if (bins.Count == 0)
            throw new FileNotFoundException(Loc.S.ErrCueReferencedBinNotFound);

        foreach (string bin in bins)
        {
            if (!File.Exists(bin))
                throw new FileNotFoundException(Loc.S.ErrBinFileMissing(bin));

            string binPath = bin;

            romfs.Add(dir + Path.GetFileName(bin), new FileInfo(bin).Length, () => new FileStream(binPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan));
        }

        romfs.WriteAllBytes(dir + cueFileName, File.ReadAllBytes(cuePath));
    }

    private static string BuildCueText(CueFile cue)
    {
        var sb = new StringBuilder();

        foreach (var entry in cue.Entries)
        {
            sb.AppendLine($"FILE \"{entry.FileName}\" {entry.FileType}");

            foreach (var track in entry.Tracks)
            {
                sb.AppendLine($"  TRACK {track.Number:D2} {track.DataType}");

                foreach (var index in track.Indexes)
                    sb.AppendLine($"    INDEX {index.Number:D2} {index.Position.Minutes:D2}:{index.Position.Seconds:D2}:{index.Position.Frames:D2}");
            }
        }

        return sb.ToString();
    }

    private static void WriteIni(VirtualRomfs romfs, string dir, string baseName, bool wideScreen, double volume)
    {
        string expected = dir + baseName + "_Switch.ini";
        string iniPath = romfs.EnumerateRelativeFiles().FirstOrDefault(p => string.Equals(p, expected, StringComparison.OrdinalIgnoreCase)) ?? expected;
        var ini = new IniHandler();

        if (romfs.Exists(iniPath))
            ini.LoadFromBytes(romfs.ReadAllBytes(iniPath));

        ini.SetValue("Screen", "WideScreen", wideScreen ? "1" : "0");
        ini.SetValue("Sound", "Volume", volume.ToString("0.00", CultureInfo.InvariantCulture));
        romfs.WriteAllBytes(iniPath, ini.ToBytes());
    }
}