using CD.Core.Constants;
using CD.Core.Interfaces;
using CD.Core.Models;

namespace CD.Core.Services.Readers;

public class CueSheetReader : IDiscImageReader
{
    private const int FramesPerSecond = 75;
    private const int FramesPerMinute = 60 * FramesPerSecond;

    public bool CanRead(string filePath)
    {
        if (!string.Equals(Path.GetExtension(filePath), ".cue", StringComparison.OrdinalIgnoreCase))
            return false;

        return File.Exists(filePath);
    }

    public DiscImage Read(string filePath)
    {
        var cueFile = CueFileReader.Read(filePath);

        if (cueFile.Entries.Count == 0)
            throw new InvalidDataException($"CUE 파일에서 FILE 항목을 찾지 못했습니다: {filePath}");

        var cueDirectory = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? string.Empty;
        var tracks = new List<DiscTrack>();

        foreach (var entry in cueFile.Entries)
        {
            if (!string.Equals(entry.FileType, "BINARY", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException($"BINARY 이외의 FILE 형식은 지원하지 않습니다 ({entry.FileType}): {entry.FileName}");

            if (entry.Tracks.Count == 0)
                throw new InvalidDataException($"FILE \"{entry.FileName}\"에 TRACK이 없습니다: {filePath}");

            tracks.AddRange(BuildTracks(entry, ResolveBinPath(cueDirectory, entry.FileName), filePath));
        }

        if (tracks.Count == 0)
            throw new InvalidDataException($"CUE 파일에서 유효한 트랙을 찾지 못했습니다: {filePath}");

        return new DiscImage { Tracks = tracks };
    }

    private static List<DiscTrack> BuildTracks(CueFileEntry entry, string binPath, string cueFilePath)
    {
        var binLength = new FileInfo(binPath).Length;
        var layouts = new List<(CueTrack Track, int SectorSize, long StartFrame, long Index1Frame, long StartOffset)>();
        long previousOffset = 0;
        long previousStartFrame = 0;
        var previousSectorSize = 0;

        foreach (var track in entry.Tracks)
        {
            var sectorSize = GetSectorSize(track.DataType);
            var index1 = track.Indexes.FirstOrDefault(i => i.Number == 1) ?? throw new InvalidDataException($"트랙 {track.Number}에 INDEX 01이 없습니다: {cueFilePath}");
            var index0 = track.Indexes.FirstOrDefault(i => i.Number == 0);
            var index1Frame = ToFrames(index1.Position);
            var startFrame = index0 is null ? index1Frame : ToFrames(index0.Position);
            var startOffset = layouts.Count == 0 ? startFrame * sectorSize : previousOffset + (startFrame - previousStartFrame) * previousSectorSize;

            layouts.Add((track, sectorSize, startFrame, index1Frame, startOffset));

            previousOffset = startOffset;
            previousStartFrame = startFrame;
            previousSectorSize = sectorSize;
        }

        var result = new List<DiscTrack>();

        for (var i = 0; i < layouts.Count; i++)
        {
            var (track, sectorSize, startFrame, index1Frame, startOffset) = layouts[i];
            var endOffset = i + 1 < layouts.Count ? layouts[i + 1].StartOffset : binLength;
            var byteLength = endOffset - startOffset;
            var totalSectors = byteLength / sectorSize;
            var pregapSectors = index1Frame - startFrame;
            var lengthSectors = totalSectors - pregapSectors;

            if (startOffset < 0 || endOffset > binLength || pregapSectors < 0 || lengthSectors <= 0)
                throw new InvalidDataException($"트랙 {track.Number}의 범위가 BIN 파일 크기를 벗어나거나 비정상입니다 (offset={startOffset}, length={byteLength}, binSize={binLength}): {cueFilePath}");

            var streamOffset = startOffset;
            var streamLength = totalSectors * sectorSize;

            result.Add(new DiscTrack
            {
                Number = track.Number,
                DataType = track.DataType,
                PregapSectors = (int)pregapSectors,
                LengthSectors = (int)lengthSectors,
                SourceSectorSize = sectorSize,
                SubchannelSize = 0,
                OpenSectorStream = () => OpenTrackStream(binPath, streamOffset, streamLength)
            });
        }

        return result;
    }

    private static string ResolveBinPath(string cueDirectory, string fileName)
    {
        var candidate = Path.IsPathRooted(fileName) ? fileName : Path.Combine(cueDirectory, fileName);

        if (File.Exists(candidate))
            return candidate;

        var sibling = Path.Combine(cueDirectory, Path.GetFileName(fileName.Replace('\\', '/')));

        if (File.Exists(sibling))
            return sibling;

        throw new FileNotFoundException($"CUE가 참조하는 BIN 파일을 찾을 수 없습니다: {fileName}");
    }

    private static long ToFrames(MsfPosition position) => position.Minutes * (long)FramesPerMinute + position.Seconds * FramesPerSecond + position.Frames;

    private static int GetSectorSize(string dataType) => dataType switch
    {
        CueFormatStrings.Audio => 2352,
        CueFormatStrings.Mode1_2048 => 2048,
        CueFormatStrings.Mode1_2352 => 2352,
        CueFormatStrings.Mode2_2336 => 2336,
        CueFormatStrings.Mode2_2352 => 2352,
        CueFormatStrings.Cdi_2336 => 2336,
        CueFormatStrings.Cdi_2352 => 2352,
        _ => throw new NotSupportedException($"지원하지 않는 트랙 타입입니다: {dataType}")
    };

    private static SubStream OpenTrackStream(string binPath, long startOffset, long length)
    {
        var fs = new FileStream(binPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        fs.Seek(startOffset, SeekOrigin.Begin);

        return new SubStream(fs, length);
    }
}