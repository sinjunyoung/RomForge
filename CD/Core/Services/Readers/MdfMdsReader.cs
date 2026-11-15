using CD.Core.Constants;
using Common.Localization;
using CD.Core.Interfaces;
using CD.Core.Models;

namespace CD.Core.Services.Readers;

public class MdfMdsReader : IDiscImageReader
{
    private const string Signature = "MEDIA DESCRIPTOR";
    private const int SessionOffsetFieldPosition = 0x50;
    private const int MinHeaderSize = SessionOffsetFieldPosition + 4;
    private const int SessionBlockSize = 0x18;
    private const int TrackBlockSize = 0x50;
    private const int OutputSectorSize = 2352;

    private const int MinRealTrackPoint = 1;
    private const int MaxRealTrackPoint = 99;

    private const byte TrackModeAudio = 0xA9;
    private const byte TrackModeMode1 = 0xAA;

    public bool CanRead(string filePath)
    {
        if (!string.Equals(Path.GetExtension(filePath), ".mds", StringComparison.OrdinalIgnoreCase))
            return false;

        return File.Exists(filePath);
    }

    public DiscImage Read(string filePath)
    {
        var mdfPath = ResolveMdfPath(filePath);
        var mdsBytes = File.ReadAllBytes(filePath);

        ValidateSignature(mdsBytes, filePath);

        var (trackBlocksOffset, totalDataBlocks) = ReadSessionInfo(mdsBytes, filePath);
        var tracks = ReadTracks(mdsBytes, trackBlocksOffset, totalDataBlocks, mdfPath, filePath);

        if (tracks.Count == 0)
            throw new InvalidDataException(Loc.S.ErrMdsNoValidTrack(filePath));

        return new DiscImage { Tracks = tracks };
    }

    private static string ResolveMdfPath(string mdsPath)
    {
        var mdfPath = Path.ChangeExtension(mdsPath, ".mdf");

        if (!File.Exists(mdfPath))
            throw new FileNotFoundException(Loc.S.ErrMdsPairedMdfNotFound(mdfPath));

        return mdfPath;
    }

    private static void ValidateSignature(byte[] mds, string filePath)
    {
        if (mds.Length < MinHeaderSize)
            throw new InvalidDataException(Loc.S.ErrMdsFileTooSmall(filePath));

        var signature = System.Text.Encoding.ASCII.GetString(mds, 0, Signature.Length);

        if (signature != Signature)
            throw new InvalidDataException(Loc.S.ErrMdsSignatureMismatch(filePath));
    }

    private static (uint TrackBlocksOffset, int TotalDataBlocks) ReadSessionInfo(byte[] mds, string filePath)
    {
        var sessionBlockOffset = BitConverter.ToUInt32(mds, SessionOffsetFieldPosition);

        if (sessionBlockOffset == 0 || (long)sessionBlockOffset + SessionBlockSize > mds.Length)
            throw new InvalidDataException(Loc.S.ErrMdsSessionBlockOffsetInvalid(sessionBlockOffset.ToString("X"), filePath));

        var totalDataBlocks = mds[sessionBlockOffset + 0x0A];
        var offset = BitConverter.ToUInt32(mds, (int)sessionBlockOffset + 0x14);

        if (offset == 0 || offset >= mds.Length)
            throw new InvalidDataException(Loc.S.ErrMdsTrackBlockOffsetInvalid(offset.ToString("X"), filePath));

        if (totalDataBlocks == 0)
            throw new InvalidDataException(Loc.S.ErrMdsSessionZeroBlocks(filePath));

        return (offset, totalDataBlocks);
    }

    private static List<DiscTrack> ReadTracks(byte[] mds, uint trackBlocksOffset, int totalDataBlocks, string mdfPath, string mdsFilePath)
    {
        var mdfLength = new FileInfo(mdfPath).Length;
        var rawTracks = new List<(int Point, byte Mode, byte SubchannelFlag, uint IndexBlockOffset, ushort SectorSize, long StartOffset)>();
        var blockAreaEnd = (int)trackBlocksOffset + totalDataBlocks * TrackBlockSize;

        if (blockAreaEnd > mds.Length)
            throw new InvalidDataException(Loc.S.ErrMdsTrackBlockAreaOutOfFile(totalDataBlocks, mdsFilePath));

        for (var pos = (int)trackBlocksOffset; pos < blockAreaEnd; pos += TrackBlockSize)
        {
            var point = mds[pos + 0x04];

            if (point < MinRealTrackPoint || point > MaxRealTrackPoint)
                continue;

            var mode = mds[pos + 0x00];
            var subchannelFlag = mds[pos + 0x01];
            var indexBlockOffset = BitConverter.ToUInt32(mds, pos + 0x0C);
            var sectorSize = BitConverter.ToUInt16(mds, pos + 0x10);
            var startOffset = BitConverter.ToInt64(mds, pos + 0x28);

            rawTracks.Add((point, mode, subchannelFlag, indexBlockOffset, sectorSize, startOffset));
        }

        var tracks = new List<DiscTrack>();

        foreach (var (Point, Mode, SubchannelFlag, IndexBlockOffset, SectorSize, StartOffset) in rawTracks.OrderBy(t => t.Point))
        {
            if (IndexBlockOffset == 0 || IndexBlockOffset + 8 > mds.Length)
                throw new InvalidDataException(Loc.S.ErrMdsIndexBlockOffsetInvalid(Point, mdsFilePath));

            var pregapSectors = (int)BitConverter.ToUInt32(mds, (int)IndexBlockOffset + 0);
            var lengthSectors = (int)BitConverter.ToUInt32(mds, (int)IndexBlockOffset + 4);
            var subchannelSize = SubchannelFlag != 0 ? Math.Max(0, SectorSize - OutputSectorSize) : 0;
            var totalSectors = pregapSectors + lengthSectors;
            var trackByteLength = (long)totalSectors * SectorSize;

            if (StartOffset < 0 || StartOffset + trackByteLength > mdfLength)
                throw new InvalidDataException(Loc.S.ErrMdsTrackDataRangeOutOfMdf(Point, StartOffset.ToString("X"), trackByteLength, mdfLength, mdsFilePath));

            var startOffset = StartOffset;
            var sourceSectorSize = SectorSize;

            tracks.Add(new DiscTrack
            {
                Number = Point,
                DataType = MapTrackMode(Mode),
                PregapSectors = pregapSectors,
                LengthSectors = lengthSectors,
                SourceSectorSize = sourceSectorSize,
                SubchannelSize = subchannelSize,
                OpenSectorStream = () => OpenTrackStream(mdfPath, startOffset, trackByteLength)
            });
        }

        return tracks;
    }

    private static SubStream OpenTrackStream(string mdfPath, long startOffset, long length)
    {
        var fs = new FileStream(mdfPath, FileMode.Open, FileAccess.Read, FileShare.Read);

        fs.Seek(startOffset, SeekOrigin.Begin);

        return new SubStream(fs, length);
    }

    public static IReadOnlyList<string> GetReferencedFileNames(string mdsPath)
    {
        var mds = File.ReadAllBytes(mdsPath);

        ValidateSignature(mds, mdsPath);

        var (trackBlocksOffset, totalDataBlocks) = ReadSessionInfo(mds, mdsPath);
        var blockAreaEnd = (int)trackBlocksOffset + totalDataBlocks * TrackBlockSize;

        if (blockAreaEnd > mds.Length)
            throw new InvalidDataException(Loc.S.ErrMdsTrackBlockAreaOutOfFile(totalDataBlocks, mdsPath));

        var names = new List<string>();

        for (var pos = (int)trackBlocksOffset; pos < blockAreaEnd; pos += TrackBlockSize)
        {
            var point = mds[pos + 0x04];

            if (point < MinRealTrackPoint || point > MaxRealTrackPoint)
                continue;

            var numFilenames = BitConverter.ToUInt32(mds, pos + 0x30);
            var filenameBlockOffset = BitConverter.ToUInt32(mds, pos + 0x34);

            names.Add(numFilenames > 0 && filenameBlockOffset > 0 && filenameBlockOffset < mds.Length ? ReadFilenameBlock(mds, filenameBlockOffset) : "*.mdf");
        }

        if (names.Count == 0)
            names.Add("*.mdf");

        return [.. names.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static string ReadFilenameBlock(byte[] mds, uint filenameBlockOffset)
    {
        if (filenameBlockOffset + 5 > mds.Length)
            return "*.mdf";

        var nameOffset = BitConverter.ToUInt32(mds, (int)filenameBlockOffset);
        var format = mds[(int)filenameBlockOffset + 4];

        if (nameOffset >= mds.Length)
            return "*.mdf";

        return format == 1 ? ReadUtf16Nul(mds, (int)nameOffset) : ReadAsciiNul(mds, (int)nameOffset);
    }

    private static string ReadAsciiNul(byte[] mds, int offset)
    {
        var end = offset;

        while (end < mds.Length && mds[end] != 0)
            end++;

        return System.Text.Encoding.ASCII.GetString(mds, offset, end - offset);
    }

    private static string ReadUtf16Nul(byte[] mds, int offset)
    {
        var end = offset;

        while (end + 1 < mds.Length && !(mds[end] == 0 && mds[end + 1] == 0))
            end += 2;

        return System.Text.Encoding.Unicode.GetString(mds, offset, end - offset);
    }

    private static string MapTrackMode(byte mode) => mode switch
    {
        TrackModeAudio => CueFormatStrings.Audio,
        TrackModeMode1 => CueFormatStrings.Mode1_2352,
        _ => CueFormatStrings.Mode2_2352
    };
}