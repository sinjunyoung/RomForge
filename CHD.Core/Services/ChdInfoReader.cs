using CHD.Core.Interop;
using CHD.Core.Interop.Enums;
using CHD.Core.Models;
using System.Text;

namespace CHD.Core.Services;

public class ChdInfoReader
{
    private const uint CDROM_OLD_METADATA = 0x43484344; // 'CHCD'
    private const uint CDROM_TRACK_METADATA = 0x43485452; // 'CHTR'
    private const uint CDROM_TRACK_METADATA2 = 0x43485432; // 'CHT2'
    private const uint GDROM_TRACK_METADATA = 0x43484754; // 'CHGT'
    private const uint GDROM_OLD_METADATA = 0x43484744; // 'CHGD'

    private const uint DVD_METADATA = 0x44564420;

    public static ChdInfo ReadChdInfo(string filePath)
    {
        using var wrapper = new LibChdrWrapper();
        var result = wrapper.Open(filePath);

        if (result != ChdrError.CHDERR_NONE)
            throw new Exception($"Failed to open CHD: {LibChdrWrapper.GetErrorString(result)}");

        if (!wrapper.Header.HasValue)
            throw new Exception("Failed to read CHD header");

        var header = wrapper.Header.Value;
        var info = new ChdInfo
        {
            FileName = Path.GetFileName(filePath),
            Version = header.version,
            LogicalBytes = header.logicalbytes,
            HunkBytes = header.hunkbytes,
            TotalHunks = header.totalhunks,
            Sha1 = ByteArrayToHex(header.sha1),
            ParentSha1 = ByteArrayToHex(header.parentsha1),
            RawSha1 = ByteArrayToHex(header.rawsha1)
        };

        var compressions = new List<string>();

        if (header.compression0 != 0)
            compressions.Add(LibChdrWrapper.GetCompressionName(header.compression0));
        if (header.compression1 != 0)
            compressions.Add(LibChdrWrapper.GetCompressionName(header.compression1));
        if (header.compression2 != 0)
            compressions.Add(LibChdrWrapper.GetCompressionName(header.compression2));
        if (header.compression3 != 0)
            compressions.Add(LibChdrWrapper.GetCompressionName(header.compression3));

        info.CompressionMethods = [.. compressions];

        DetectSourceType(wrapper, info);

        return info;
    }

    private static void DetectSourceType(LibChdrWrapper wrapper, ChdInfo info)
    {
        var dvdMetadata = wrapper.GetMetadata(DVD_METADATA, 0);

        if (dvdMetadata != null)
        {
            info.SourceType = ChdSourceType.DVD;
            return;
        }

        var gdromMetadata = wrapper.GetMetadata(GDROM_TRACK_METADATA, 0);
        gdromMetadata ??= wrapper.GetMetadata(GDROM_OLD_METADATA, 0);

        if (!string.IsNullOrEmpty(gdromMetadata))
        {
            ParseCdromMetadata(wrapper, info);
            info.SourceType = ChdSourceType.GdRom;
            return;
        }

        var trackMetadata = wrapper.GetMetadata(CDROM_TRACK_METADATA2, 0);

        trackMetadata ??= wrapper.GetMetadata(CDROM_TRACK_METADATA, 0);

        if (!string.IsNullOrEmpty(trackMetadata))
        {
            ParseCdromMetadata(wrapper, info);

            if (info.TrackCount == 1)
                info.SourceType = ChdSourceType.ISO;
            else
                info.SourceType = ChdSourceType.BinCue;
        }
    }

    private static void ParseCdromMetadata(LibChdrWrapper wrapper, ChdInfo info)
    {
        var tracks = new List<TrackInfo>();
        uint trackIndex = 0;
        long physofs = 0, chdofs = 0, logofs = 0;

        while (true)
        {
            var metadata = wrapper.GetMetadata(CDROM_TRACK_METADATA2, trackIndex);
            metadata ??= wrapper.GetMetadata(CDROM_TRACK_METADATA, trackIndex);
            metadata ??= wrapper.GetMetadata(GDROM_TRACK_METADATA, trackIndex);
            metadata ??= wrapper.GetMetadata(GDROM_OLD_METADATA, trackIndex);

            if (string.IsNullOrEmpty(metadata))
                break;

            var track = ParseTrackMetadata(metadata, trackIndex);
            if (track == null) break;

            int padded = (track.Frames + 4 - 1) / 4 * 4;
            track.ExtraFrames = padded - track.Frames;

            track.LogFrameOfs = 0;
            if (track.PgDataSize == 0)
                logofs += track.PreGap;
            else
                track.LogFrameOfs = track.PreGap;

            track.PhysFrameOfs = physofs;
            track.ChdFrameOfs = chdofs;
            track.LogFrameOfs += logofs;
            track.LogFrames = track.Frames - track.PreGap;

            logofs += track.PostGap;
            physofs += track.Frames;
            chdofs += track.Frames + track.ExtraFrames;
            logofs += track.Frames;

            tracks.Add(track);
            trackIndex++;
        }

        tracks.Add(new TrackInfo { PhysFrameOfs = physofs, LogFrameOfs = logofs, ChdFrameOfs = chdofs });

        info.Tracks = [.. tracks];
        info.TrackCount = tracks.Count - 1;
    }

    private static TrackInfo ParseTrackMetadata(string metadata, uint trackIndex)
    {
        var parts = metadata.Split([' '], StringSplitOptions.RemoveEmptyEntries);
        var track = new TrackInfo
        {
            TrackNumber = (int)trackIndex + 1
        };

        foreach (var part in parts)
        {
            var kv = part.Split(':');
            if (kv.Length != 2) continue;

            switch (kv[0].ToUpperInvariant())
            {
                case "TYPE":
                    track.TrackType = kv[1];
                    break;
                case "SUBTYPE":
                    track.SubType = kv[1];
                    break;
                case "FRAMES":
                    _ = int.TryParse(kv[1], out int frames);
                    track.Frames = frames;
                    break;
                case "PREGAP":
                    _ = int.TryParse(kv[1], out int pregap);
                    track.PreGap = pregap;
                    break;
                case "POSTGAP":
                    _ = int.TryParse(kv[1], out int postgap);
                    track.PostGap = postgap;
                    break;
            }
        }

        return track;
    }

    private static string ByteArrayToHex(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return string.Empty;

        var sb = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
            sb.AppendFormat("{0:x2}", b);

        return sb.ToString();
    }
}