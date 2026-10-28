using CD.Core.Constants;
using CD.Core.Interfaces;
using CD.Core.Models;
using System.Text.RegularExpressions;

namespace CD.Core.Services.Readers;

public partial class CcdImgSubReader : IDiscImageReader
{
    private const int SectorSize = 2352;

    [GeneratedRegex(@"^\[TRACK\s+\d+\]$", RegexOptions.IgnoreCase)]
    private static partial Regex TrackHeaderRegex();

    [GeneratedRegex(@"^MODE\s*=\s*(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ModeRegex();

    [GeneratedRegex(@"^INDEX\s+(\d+)\s*=\s*(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex IndexRegex();

    [GeneratedRegex(@"^\[Entry\s+\d+\]$", RegexOptions.IgnoreCase)]
    private static partial Regex EntryHeaderRegex();

    [GeneratedRegex(@"^Point\s*=\s*0x([0-9A-Fa-f]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex PointRegex();

    [GeneratedRegex(@"^Control\s*=\s*0x([0-9A-Fa-f]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ControlRegex();

    [GeneratedRegex(@"^PLBA\s*=\s*(-?\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex PlbaRegex();

    public bool CanRead(string filePath)
    {
        if (!string.Equals(Path.GetExtension(filePath), ".ccd", StringComparison.OrdinalIgnoreCase))
            return false;

        return File.Exists(filePath);
    }

    public DiscImage Read(string filePath)
    {
        var imgPath = ResolveSiblingPath(filePath, ".img");
        var lines = File.ReadAllLines(filePath);
        var imgLength = new FileInfo(imgPath).Length;

        if (imgLength % SectorSize != 0)
            throw new InvalidDataException($"IMG 파일 크기가 섹터 크기(2352)의 배수가 아닙니다: {imgPath}");

        var totalImgSectors = imgLength / SectorSize;
        var rawTracks = ParseTracks(lines);
        List<DiscTrack> tracks;

        if (rawTracks.Count > 0)
            tracks = BuildDiscTracks(rawTracks, imgPath, totalImgSectors, filePath);
        else
        {
            var entries = ParseEntries(lines);

            if (entries.Count == 0)
                throw new InvalidDataException($"CCD 파일에서 유효한 [TRACK] 또는 [Entry] 섹션을 찾지 못했습니다: {filePath}");

            tracks = BuildDiscTracksFromEntries(entries, imgPath, totalImgSectors, filePath);
        }

        return new DiscImage { Tracks = tracks };
    }

    private static string ResolveSiblingPath(string ccdPath, string extension)
    {
        var siblingPath = Path.ChangeExtension(ccdPath, extension);

        if (!File.Exists(siblingPath))
            throw new FileNotFoundException($"CCD와 짝을 이루는 {extension.ToUpperInvariant()} 파일을 찾을 수 없습니다: {siblingPath}");

        return siblingPath;
    }

    private static List<(int Mode, long? Index0, long Index1)> ParseTracks(string[] lines)
    {
        var tracks = new List<(int Mode, long? Index0, long Index1)>();
        var inTrack = false;
        var mode = -1;
        long? index0 = null;
        long? index1 = null;

        void FlushCurrent()
        {
            if (!inTrack)
                return;

            if (mode < 0 || index1 is null)
                throw new InvalidDataException("CCD 파일의 TRACK 섹션에 MODE 또는 INDEX 1이 누락되었습니다.");

            tracks.Add((mode, index0, index1.Value));
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (TrackHeaderRegex().IsMatch(line))
            {
                FlushCurrent();

                inTrack = true;
                mode = -1;
                index0 = null;
                index1 = null;

                continue;
            }

            if (!inTrack)
                continue;

            var modeMatch = ModeRegex().Match(line);

            if (modeMatch.Success)
            {
                mode = int.Parse(modeMatch.Groups[1].Value);
                continue;
            }

            var indexMatch = IndexRegex().Match(line);

            if (indexMatch.Success)
            {
                var indexNumber = int.Parse(indexMatch.Groups[1].Value);
                var indexValue = long.Parse(indexMatch.Groups[2].Value);

                if (indexNumber == 0)
                    index0 = indexValue;
                else if (indexNumber == 1)
                    index1 = indexValue;
            }
        }

        FlushCurrent();

        return tracks;
    }

    private static List<DiscTrack> BuildDiscTracks(List<(int Mode, long? Index0, long Index1)> rawTracks, string imgPath, long totalImgSectors, string ccdFilePath)
    {
        var tracks = new List<DiscTrack>();

        for (var i = 0; i < rawTracks.Count; i++)
        {
            var (mode, index0, index1) = rawTracks[i];

            var trackStart = index0 ?? index1;
            var pregapSectors = index0.HasValue ? index1 - index0.Value : 0;
            var nextStart = i + 1 < rawTracks.Count ? (rawTracks[i + 1].Index0 ?? rawTracks[i + 1].Index1) : totalImgSectors;
            var lengthSectors = nextStart - index1;

            if (trackStart < 0 || nextStart > totalImgSectors || lengthSectors <= 0)
                throw new InvalidDataException($"트랙 {i + 1}의 섹터 범위가 IMG 파일 크기를 벗어나거나 비정상입니다 (start={trackStart}, length={lengthSectors}, imgSectors={totalImgSectors}): {ccdFilePath}");

            var streamOffset = trackStart * SectorSize;
            var streamLength = (nextStart - trackStart) * SectorSize;

            tracks.Add(new DiscTrack
            {
                Number = i + 1,
                DataType = MapTrackMode(mode),
                PregapSectors = (int)pregapSectors,
                LengthSectors = (int)lengthSectors,
                SourceSectorSize = SectorSize,
                SubchannelSize = 0,
                OpenSectorStream = () => OpenTrackStream(imgPath, streamOffset, streamLength)
            });
        }

        return tracks;
    }

    private static List<DiscTrack> BuildDiscTracksFromEntries(List<(int Point, byte Control, long Plba)> entries, string imgPath, long totalImgSectors, string ccdFilePath)
    {
        var tracks = new List<DiscTrack>();

        for (var i = 0; i < entries.Count; i++)
        {
            var (point, control, plba) = entries[i];

            var trackStart = plba;
            var nextStart = i + 1 < entries.Count ? entries[i + 1].Plba : totalImgSectors;
            var lengthSectors = nextStart - trackStart;

            if (trackStart < 0 || nextStart > totalImgSectors || lengthSectors <= 0)
                throw new InvalidDataException($"Entry 기반 트랙 {i + 1}의 섹터 범위가 IMG 파일 크기를 벗어나거나 비정상입니다 (start={trackStart}, length={lengthSectors}, imgSectors={totalImgSectors}): {ccdFilePath}");

            var isData = (control & 0x04) != 0;
            var dataType = isData ? MapTrackMode(DetectDataMode(imgPath, trackStart)) : CueFormatStrings.Audio;
            var streamOffset = trackStart * SectorSize;
            var streamLength = lengthSectors * SectorSize;

            tracks.Add(new DiscTrack
            {
                Number = point > 0 ? point : i + 1,
                DataType = dataType,
                PregapSectors = 0,
                LengthSectors = (int)lengthSectors,
                SourceSectorSize = SectorSize,
                SubchannelSize = 0,
                OpenSectorStream = () => OpenTrackStream(imgPath, streamOffset, streamLength)
            });
        }

        return tracks;
    }

    private static List<(int Point, byte Control, long Plba)> ParseEntries(string[] lines)
    {
        var entries = new List<(int Point, byte Control, long Plba)>();
        var inEntry = false;
        int point = -1;
        byte control = 0;
        long? plba = null;

        void FlushCurrent()
        {
            if (!inEntry || plba is null)
                return;

            if (point is >= 1 and <= 99)
                entries.Add((point, control, plba.Value));
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            if (EntryHeaderRegex().IsMatch(line))
            {
                FlushCurrent();

                inEntry = true;
                point = -1;
                control = 0;
                plba = null;

                continue;
            }

            if (!inEntry)
                continue;

            var pointMatch = PointRegex().Match(line);

            if (pointMatch.Success)
            {
                point = Convert.ToInt32(pointMatch.Groups[1].Value, 16);
                continue;
            }

            var controlMatch = ControlRegex().Match(line);

            if (controlMatch.Success)
            {
                control = Convert.ToByte(controlMatch.Groups[1].Value, 16);
                continue;
            }

            var plbaMatch = PlbaRegex().Match(line);

            if (plbaMatch.Success)
                plba = long.Parse(plbaMatch.Groups[1].Value);
        }

        FlushCurrent();

        return [.. entries.OrderBy(e => e.Point)];
    }

    private static int DetectDataMode(string imgPath, long lba)
    {
        using var fs = new FileStream(imgPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        fs.Seek(lba * SectorSize, SeekOrigin.Begin);

        var header = new byte[16];
        var read = fs.Read(header, 0, header.Length);

        return read >= 16 && header[15] == 2 ? 2 : 1;
    }

    private static SubStream OpenTrackStream(string imgPath, long startOffset, long length)
    {
        var fs = new FileStream(imgPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        fs.Seek(startOffset, SeekOrigin.Begin);

        return new SubStream(fs, length);
    }

    private static string MapTrackMode(int mode) => mode switch
    {
        0 => CueFormatStrings.Audio,
        1 => CueFormatStrings.Mode1_2352,
        2 => CueFormatStrings.Mode2_2352,
        _ => throw new InvalidDataException($"알 수 없는 CCD 트랙 MODE 값입니다: {mode}")
    };
}