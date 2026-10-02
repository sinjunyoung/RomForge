using CHD.Core.Interop;
using CHD.Core.Interop.Enums;
using CHD.Core.Models;
using CHD.Core.Services;
using System.IO;

namespace RomForge.Core.Services.Patch;

public static class ChdHasher
{
    private const int CdFrameSize = 2448;

    public static Task<(string Name, RomHashResult Hashes)> HashAsync(string chdPath, IProgress<double>? progress, CancellationToken ct) => Task.Run(() => Hash(chdPath, progress, ct), ct);

    private static (string Name, RomHashResult Hashes) Hash(string chdPath, IProgress<double>? progress, CancellationToken ct)
    {
        var info = ChdInfoReader.ReadChdInfo(chdPath);

        if (info.HasParent)
            throw new NotSupportedException("부모 CHD가 필요한 파일은 해시를 계산할 수 없습니다.");

        string baseName = Path.GetFileNameWithoutExtension(chdPath);
        using var chd = new LibChdrWrapper();
        var error = chd.Open(chdPath);

        if (error != ChdrError.CHDERR_NONE)
            throw new InvalidOperationException($"CHD를 열 수 없습니다: {LibChdrWrapper.GetErrorString(error)}");

        return info.SourceType switch
        {
            ChdSourceType.GdRom => HashTrack(chd, info, SelectDataTrack(info, true), true, progress, ct),
            ChdSourceType.ISO or ChdSourceType.BinCue => HashTrack(chd, info, SelectDataTrack(info, false), false, progress, ct),
            ChdSourceType.DVD => (baseName + ".iso", HashLogical(chd, progress, ct)),
            _ => (baseName + ".img", HashLogical(chd, progress, ct))
        };
    }

    private static TrackInfo SelectDataTrack(ChdInfo info, bool lastDataTrack)
    {
        var tracks = info.Tracks.Take(info.TrackCount).ToList();
        var dataTracks = tracks.Where(t => !string.Equals(t.TrackType, "AUDIO", StringComparison.OrdinalIgnoreCase)).ToList();

        if (dataTracks.Count == 0)
            return tracks[0];

        return lastDataTrack ? dataTracks[^1] : dataTracks[0];
    }

    private static (string Name, RomHashResult Hashes) HashTrack(LibChdrWrapper chd, ChdInfo info, TrackInfo track, bool gdiNaming, IProgress<double>? progress, CancellationToken ct)
    {
        int dataSize = GetDataSize(track.TrackType);
        long frameCount = Math.Max(0, track.Frames - track.PadFrames);
        int hunkBytes = (int)chd.Header!.Value.hunkbytes;
        var hunk = new byte[hunkBytes];
        uint cachedHunk = uint.MaxValue;
        using var sink = new RomHashSink(frameCount * dataSize, progress, ct);

        for (long frame = 0; frame < frameCount; frame++)
        {
            long byteOffset = (track.ChdFrameOfs + frame) * CdFrameSize;
            uint hunkIndex = (uint)(byteOffset / hunkBytes);
            int offsetInHunk = (int)(byteOffset % hunkBytes);

            if (offsetInHunk + dataSize > hunkBytes)
                throw new NotSupportedException("지원하지 않는 CHD 헌크 크기입니다.");

            if (hunkIndex != cachedHunk)
            {
                chd.ReadHunkInto(hunkIndex, hunk);
                cachedHunk = hunkIndex;
            }

            sink.Write(new ReadOnlySpan<byte>(hunk, offsetInHunk, dataSize));
        }

        var result = sink.Complete();

        return (BuildTrackName(info, track, gdiNaming), result);
    }

    private static string BuildTrackName(ChdInfo info, TrackInfo track, bool gdiNaming)
    {
        string baseName = Path.GetFileNameWithoutExtension(info.FileName);

        if (gdiNaming)
            return $"{baseName}{track.TrackNumber:00}.bin";

        if (info.TrackCount == 1)
            return baseName + (track.TrackType.StartsWith("MODE1", StringComparison.OrdinalIgnoreCase) ? ".iso" : ".bin");

        string number = info.TrackCount >= 10 ? track.TrackNumber.ToString("00") : track.TrackNumber.ToString();

        return $"{baseName} (Track {number}).bin";
    }

    private static RomHashResult HashLogical(LibChdrWrapper chd, IProgress<double>? progress, CancellationToken ct)
    {
        var header = chd.Header!.Value;
        int hunkBytes = (int)header.hunkbytes;
        long totalBytes = (long)header.logicalbytes;
        var hunk = new byte[hunkBytes];
        using var sink = new RomHashSink(totalBytes, progress, ct);
        long remaining = totalBytes;

        for (uint hunkIndex = 0; remaining > 0; hunkIndex++)
        {
            chd.ReadHunkInto(hunkIndex, hunk);

            int length = (int)Math.Min(hunkBytes, remaining);

            sink.Write(new ReadOnlySpan<byte>(hunk, 0, length));

            remaining -= length;
        }

        return sink.Complete();
    }

    private static int GetDataSize(string trackType) => trackType.ToUpperInvariant() switch
    {
        "MODE1" or "MODE1/2048" or "MODE2_FORM1" or "MODE2/2048" => 2048,
        "MODE2" or "MODE2/2336" or "MODE2_FORM_MIX" => 2336,
        "MODE2_FORM2" or "MODE2/2324" => 2324,
        "MODE1_RAW" or "MODE1/2352" or "MODE2_RAW" or "MODE2/2352" or "CDI/2352" or "AUDIO" => 2352,
        _ => throw new NotSupportedException($"지원하지 않는 CHD 트랙 타입입니다: {trackType}")
    };
}