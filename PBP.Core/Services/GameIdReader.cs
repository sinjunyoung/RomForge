using CHD.Core.Interop;
using CHD.Core.Interop.Enums;
using PBP.Core.Enums;
using PBP.Core.Models;

namespace PBP.Core.Services;

public static class GameIdReader
{
    private const string Fallback = "SLUS00000";

    public static string ReadFromDisk(DiskSource source)
    {
        try
        {
            return source.Type == DiskSourceType.Chd
                ? ReadFromChd(source.FilePath)
                : ReadFromFilePath(source.FilePath);
        }
        catch
        {
            return Fallback;
        }
    }

    public static string ReadFromStream(Stream stream, long length)
    {
        try
        {
            var isRaw = length >= 2352 && length % 2352 == 0;

            byte[] SectorReader(uint lba)
            {
                var sector = new byte[2048];
                stream.Seek(isRaw ? lba * 2352 + 24 : lba * 2048, SeekOrigin.Begin);
                stream.Read(sector, 0, 2048);
                return sector;
            }

            return Iso9660GameIdExtractor.Extract(SectorReader) ?? Fallback;
        }
        catch
        {
            return Fallback;
        }
    }

    private static string ReadFromFilePath(string filePath)
    {
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ReadFromStream(stream, stream.Length);
    }
    private static string ReadFromChd(string filePath)
    {
        using var wrapper = new LibChdrWrapper();
        wrapper.Open(filePath, ChdrOpenFlags.CHDOPEN_READ);

        var fileOffset = GetTrack1FileOffset(wrapper);
        var sectorsPerHunk = wrapper.Header!.Value.hunkbytes / 2448u;

        byte[] SectorReader(uint lba)
        {
            var frame = fileOffset + lba;
            var hunkIndex = frame / sectorsPerHunk;
            var hunkOffset = (frame % sectorsPerHunk) * 2448u;
            var hunk = wrapper.ReadHunk(hunkIndex);
            var sector = new byte[2048];

            Buffer.BlockCopy(hunk, (int)hunkOffset + 24, sector, 0, 2048);

            return sector;
        }

        return Iso9660GameIdExtractor.Extract(SectorReader) ?? Fallback;
    }

    private static uint GetTrack1FileOffset(LibChdrWrapper chd)
    {
        var meta = chd.GetMetadata(0x54524B32, 0);

        if (meta != null)
        {
            var m = System.Text.RegularExpressions.Regex.Match(meta,
                @"TRACK:(\d+) TYPE:(\S+) SUBTYPE:(\S+) FRAMES:(\d+) PREGAP:(\d+) PGTYPE:(\S+) PGSUB:(\S+) POSTGAP:(\d+)");

            if (m.Success)
            {
                var pregapFrames = int.Parse(m.Groups[5].Value);

                if (pregapFrames > 0 && m.Groups[6].Value.StartsWith('V'))
                    return (uint)pregapFrames;
            }
        }
        return 0u;
    }
}