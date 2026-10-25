using Patch.Core.Formats.DCP.Models;

namespace Patch.Core.Formats.DCP.Services;

public class GdRomCompositeSectorReader : IDisposable
{
    private readonly List<(GdiTrack Track, FileStream Stream, int HeaderOffset)> _dataTracks;

    public GdRomCompositeSectorReader(GdiFile gdi)
    {
        _dataTracks = [.. gdi.Tracks
            .Where(t => t.Type == TrackType.Data && t.StartLba >= 45000)
            .OrderBy(t => t.StartLba)
            .Select(t =>
            {
                var stream = new FileStream(gdi.GetTrackFullPath(t), FileMode.Open, FileAccess.Read, FileShare.Read);
                int headerOffset = t.SectorSize == 2352 ? DetectHeaderOffset(stream, t.FileOffset) : 0;

                return (Track: t, Stream: stream, HeaderOffset: headerOffset);
            })];

        if (_dataTracks.Count == 0)
            throw new InvalidDataException("High-Density 영역에서 데이터트랙을 찾을 수 없습니다.");
    }

    public uint PvdAbsoluteLba => (uint)_dataTracks[0].Track.StartLba + 16;

    public byte[] ReadSector(uint absoluteLba)
    {
        var entry = _dataTracks.LastOrDefault(t => t.Track.StartLba <= absoluteLba);

        if (entry.Stream == null)
            throw new ArgumentOutOfRangeException(nameof(absoluteLba), $"절대 LBA {absoluteLba}를 포함하는 데이터트랙을 찾을 수 없습니다.");

        uint relativeLba = absoluteLba - (uint)entry.Track.StartLba;
        long byteOffset = entry.Track.FileOffset + (long)relativeLba * entry.Track.SectorSize + entry.HeaderOffset;
        var buffer = new byte[2048];

        entry.Stream.Seek(byteOffset, SeekOrigin.Begin);

        int readTotal = 0;

        while (readTotal < 2048)
        {
            int read = entry.Stream.Read(buffer, readTotal, 2048 - readTotal);

            if (read == 0)
                throw new EndOfStreamException($"절대 LBA {absoluteLba} 섹터를 읽는 중 파일이 끝났습니다.");

            readTotal += read;
        }

        return buffer;
    }

    public Func<uint, byte[]> AsFunc() => ReadSector;

    private static int DetectHeaderOffset(FileStream stream, long fileOffset)
    {
        var rawFirstSector = new byte[2352];

        stream.Seek(fileOffset, SeekOrigin.Begin);
        stream.ReadExactly(rawFirstSector);
        stream.Seek(0, SeekOrigin.Begin);

        bool hasSync = rawFirstSector[0] == 0x00 && rawFirstSector[11] == 0x00;

        for (int i = 1; i <= 10; i++)
            hasSync &= rawFirstSector[i] == 0xFF;

        if (!hasSync)
            return 0;

        byte mode = rawFirstSector[15];

        return mode switch
        {
            1 => 16,
            2 => 24,
            _ => 16
        };
    }

    public void Dispose()
    {
        foreach (var (_, stream, _) in _dataTracks)
            stream.Dispose();

        GC.SuppressFinalize(this);
    }
}