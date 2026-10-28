using CHD.Core.Interop;
using CHD.Core.Interop.Enums;
using PSP.PBP.Enums;
using PSP.PBP.Models;
using System.Text.RegularExpressions;
using static PSP.PBP.Services.Iso9660GameIdExtractor;

namespace PSP.PBP.Services;

public static partial class GameIdReader
{
    private const string Fallback = "SLUS00000";

    [GeneratedRegex(@"TRACK:(\d+) TYPE:(\S+) SUBTYPE:(\S+) FRAMES:(\d+) PREGAP:(\d+) PGTYPE:(\S+) PGSUB:(\S+) POSTGAP:(\d+)")]
    private static partial Regex TrackMetaRegex();

    public static string ReadFromDisk(DiskSource source) => source.Type == DiskSourceType.Chd ? ReadFromChd(source.FilePath) : ReadFromFilePath(source.FilePath);

    public static string ReadFromStream(Stream stream, long length)
    {
        var isRaw = length >= 2352 && length % 2352 == 0;

        byte[] SectorReader(uint lba)
        {
            var sector = new byte[2048];

            stream.Seek(isRaw ? lba * 2352 + 24 : lba * 2048, SeekOrigin.Begin);
            stream.ReadExactly(sector, 0, 2048);

            return sector;
        }

        return HandleExtractResult(Extract(SectorReader));
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

        return HandleExtractResult(Extract(SectorReader));
    }

    private static string HandleExtractResult((string? Id, ExtractResult Result) extraction)
    {
        return extraction.Result switch
        {
            ExtractResult.Success => extraction.Id!,
            ExtractResult.NotPs1Disc => throw new NotSupportedException("PS1 게임만 지원합니다."),
            ExtractResult.InvalidDisc => throw new InvalidOperationException("디스크를 읽을 수 없습니다."),
            _ => Fallback
        };
    }

    public static uint GetTrack1FileOffset(LibChdrWrapper chd)
    {
        int index = 0;
        string? meta;

        while ((meta = chd.GetMetadata(0x54524B32, (uint)index++)) != null)
        {
            var m = TrackMetaRegex().Match(meta);

            if (m.Success)
            {
                var trackNum = int.Parse(m.Groups[1].Value);

                if (trackNum == 1)
                {
                    var pregapFrames = int.Parse(m.Groups[5].Value);

                    if (pregapFrames > 0 && m.Groups[6].Value.StartsWith('V'))
                        return (uint)pregapFrames;

                    break;
                }
            }
        }

        return 0u;
    }
}