using CHD.Core.Interop;
using CHD.Core.Interop.Enums;
using CHD.Core.Models;
using CHD.Core.Services;
using RomForge.Core.Models.Patch;
using System.IO;

namespace RomForge.Core.Services.Patch;

public static class ChdHasher
{
    private const int CdFrameSize = 2448;
    private const int HighDensityStartLba = 45000;

    public static Task<bool> NeedsGdRomLayoutChoiceAsync(string chdPath, CancellationToken ct) =>
        Task.Run(() =>
        {
            var info = ChdInfoReader.ReadChdInfo(chdPath);

            if (info.HasParent || info.SourceType != ChdSourceType.GdRom)
                return false;

            var track = SelectDataTrack(info, true);
            int index = info.Tracks.Take(info.TrackCount).ToList().IndexOf(track);
            var layout = BuildRedumpLayout(info);

            return layout is not null && index >= 0 && IsRedumpLayoutDifferent(layout[index], track);
        }, ct);

    public static Task<(string Name, RomHashResult Hashes)> HashAsync(string chdPath, GdRomHashLayout gdRomLayout, IProgress<double>? progress, CancellationToken ct) =>
        Task.Run(() => Hash(chdPath, gdRomLayout, progress, ct), ct);

    private static (string Name, RomHashResult Hashes) Hash(string chdPath, GdRomHashLayout gdRomLayout, IProgress<double>? progress, CancellationToken ct)
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
            ChdSourceType.GdRom => HashGdRom(chd, info, gdRomLayout, progress, ct),
            ChdSourceType.ISO or ChdSourceType.BinCue => HashTrack(chd, info, SelectDataTrack(info, false), false, progress, ct),
            ChdSourceType.DVD => (baseName + ".iso", HashLogical(chd, progress, ct)),
            _ => (baseName + ".img", HashLogical(chd, progress, ct)),
        };
    }

    private static (string Name, RomHashResult Hashes) HashGdRom(LibChdrWrapper chd, ChdInfo info, GdRomHashLayout gdRomLayout, IProgress<double>? progress, CancellationToken ct)
    {
        var track = SelectDataTrack(info, true);

        if (gdRomLayout == GdRomHashLayout.Cue)
        {
            int index = info.Tracks.Take(info.TrackCount).ToList().IndexOf(track);
            var layout = BuildRedumpLayout(info);

            if (layout is not null && index >= 0)
                return HashRedumpTrack(chd, info, layout, index, track.TrackNumber, progress, ct);
        }

        return HashTrack(chd, info, track, true, progress, ct);
    }

    private static TrackInfo SelectDataTrack(ChdInfo info, bool lastDataTrack)
    {
        var tracks = info.Tracks.Take(info.TrackCount).ToList();
        var dataTracks = tracks.Where(t => !IsAudio(t.TrackType)).ToList();

        if (dataTracks.Count == 0)
            return tracks[0];

        return lastDataTrack ? dataTracks[^1] : dataTracks[0];
    }

    private static bool IsAudio(string trackType) => string.Equals(trackType, "AUDIO", StringComparison.OrdinalIgnoreCase);

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
            ReadFrame(chd, track.ChdFrameOfs + frame, dataSize, hunk, hunkBytes, ref cachedHunk, out int offsetInHunk);

            sink.Write(new ReadOnlySpan<byte>(hunk, offsetInHunk, dataSize));
        }

        var result = sink.Complete();

        return (BuildTrackName(info, track, gdiNaming), result);
    }

    private static void ReadFrame(LibChdrWrapper chd, long chdFrame, int dataSize, byte[] hunk, int hunkBytes, ref uint cachedHunk, out int offsetInHunk)
    {
        long byteOffset = chdFrame * CdFrameSize;
        uint hunkIndex = (uint)(byteOffset / hunkBytes);

        offsetInHunk = (int)(byteOffset % hunkBytes);

        if (offsetInHunk + dataSize > hunkBytes)
            throw new NotSupportedException("지원하지 않는 CHD 헌크 크기입니다.");

        if (hunkIndex != cachedHunk)
        {
            chd.ReadHunkInto(hunkIndex, hunk);
            cachedHunk = hunkIndex;
        }
    }

    private delegate void FrameWriter(ReadOnlySpan<byte> frame);

    private sealed class RestoreTrack
    {
        public int Frames { get; set; }

        public int PadFrames { get; set; }

        public int SplitFrames { get; set; }

        public int DataSize { get; set; }

        public bool IsAudio { get; set; }

        public bool HasPregapData { get; set; }

        public long ChdFrameOfs { get; set; }

        public long PhysFrameOfs { get; set; }
    }

    private static List<RestoreTrack>? BuildRedumpLayout(ChdInfo info)
    {
        var tracks = info.Tracks.Take(info.TrackCount).Select(t => new RestoreTrack
        {
            Frames = t.Frames,
            PadFrames = t.PadFrames,
            DataSize = GetDataSize(t.TrackType),
            IsAudio = IsAudio(t.TrackType),
            HasPregapData = t.PreGap > 0,
            ChdFrameOfs = t.ChdFrameOfs,
            PhysFrameOfs = t.PhysFrameOfs
        }).ToList();

        if (tracks.Count < 2)
            return null;

        bool hasPhysicalPregap = tracks[0].PadFrames == 0;

        for (int i = 1; i < tracks.Count; i++)
        {
            if (tracks[i].HasPregapData)
                break;

            if (tracks[i].PhysFrameOfs == HighDensityStartLba)
                continue;

            bool isLast = i + 1 >= tracks.Count;

            if (!hasPhysicalPregap)
            {
                if (isLast && !tracks[i].IsAudio)
                {
                    if (!tracks[i - 1].IsAudio)
                    {
                        tracks[i - 1].PadFrames += 225;
                        tracks[i].SplitFrames = 225;
                        tracks[i].HasPregapData = true;
                    }
                    else
                        tracks[i - 1].Frames -= 75;
                }
            }
            else
            {
                int extra = 150;

                if (isLast && !tracks[i].IsAudio)
                    extra += 75;

                tracks[i - 1].PadFrames = extra;
                tracks[i].SplitFrames = extra;
                tracks[i].HasPregapData = true;
            }
        }

        return tracks;
    }

    private static bool IsRedumpLayoutDifferent(RestoreTrack restored, TrackInfo original) => restored.SplitFrames != 0 || restored.PadFrames != original.PadFrames || restored.Frames != original.Frames;

    private static (string Name, RomHashResult Hashes) HashRedumpTrack(LibChdrWrapper chd, ChdInfo info, List<RestoreTrack> layout, int index, int trackNumber, IProgress<double>? progress, CancellationToken ct)
    {
        var target = layout[index];
        long frameCount = Math.Max(0, target.Frames - target.PadFrames + target.SplitFrames);
        using var sink = new RomHashSink(frameCount * target.DataSize, progress, ct);

        StreamRestoredTrack(chd, layout, index, span => sink.Write(span), ct);

        var result = sink.Complete();
        string baseName = Path.GetFileNameWithoutExtension(info.FileName);
        string number = layout.Count >= 10 ? trackNumber.ToString("00") : trackNumber.ToString();

        return ($"{baseName} (Track {number}).bin", result);
    }

    private static void StreamRestoredTrack(LibChdrWrapper chd, List<RestoreTrack> layout, int index, FrameWriter write, CancellationToken ct)
    {
        var target = layout[index];
        long frameCount = Math.Max(0, target.Frames - target.PadFrames + target.SplitFrames);
        int hunkBytes = (int)chd.Header!.Value.hunkbytes;
        var hunk = new byte[hunkBytes];
        var swapped = new byte[CdFrameSize];
        uint cachedHunk = uint.MaxValue;

        for (long frame = 0; frame < frameCount; frame++)
        {
            ct.ThrowIfCancellationRequested();

            int sourceIndex;
            long frameOfs;

            if (index > 0 && frame < target.SplitFrames)
            {
                sourceIndex = index - 1;
                frameOfs = layout[sourceIndex].Frames - target.SplitFrames + frame;
            }
            else
            {
                sourceIndex = index;
                frameOfs = frame - target.SplitFrames;
            }

            var source = layout[sourceIndex];

            if (frameOfs < 0 || frameOfs >= source.Frames)
                throw new NotSupportedException("지원하지 않는 GD-ROM 트랙 배치입니다.");

            ReadFrame(chd, source.ChdFrameOfs + frameOfs, source.DataSize, hunk, hunkBytes, ref cachedHunk, out int offsetInHunk);

            if (source.IsAudio)
            {
                for (int i = 0; i + 1 < source.DataSize; i += 2)
                {
                    swapped[i] = hunk[offsetInHunk + i + 1];
                    swapped[i + 1] = hunk[offsetInHunk + i];
                }

                write(new ReadOnlySpan<byte>(swapped, 0, source.DataSize));
            }
            else
                write(new ReadOnlySpan<byte>(hunk, offsetInHunk, source.DataSize));
        }
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