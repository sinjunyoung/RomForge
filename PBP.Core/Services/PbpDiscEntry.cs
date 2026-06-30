using PBP.Core.Enums;
using PBP.Core.Models;

namespace PBP.Core.Services;

public class PbpDiscEntry : IDisposable, IAsyncDisposable
{
    const int MAX_INDEXES = 0x7E00;
    const uint PSAR_GAMEID_OFFSET = 0x400;
    const uint PSAR_TOC_OFFSET = 0x800;
    const uint PSAR_INDEX_OFFSET = 0x4000;
    const uint PSAR_ISO_OFFSET = 0x100000;
    public const int ISO_BLOCK_SIZE = 0x930;

    public Action<uint>? ProgressEvent { get; set; }

    private readonly Stream _stream;
    private readonly int _psarOffset;
    private readonly byte[] _compressionInBuffer;

    public List<IsoBlock> IsoIndex { get; }
    public List<TocEntry> TOC { get; }
    public uint IsoSize { get; }
    public int Index { get; }
    public string DiscID { get; }
    public bool IsPvdMismatch { get; }

    public PbpDiscEntry(Stream stream, int psarOffset, int index)
    {
        _stream = stream;
        Index = index;
        _psarOffset = psarOffset;
        DiscID = GetDiscID();
        TOC = ReadTOC();
        IsoIndex = ReadIsoIndexes();
        _compressionInBuffer = new byte[16 * ISO_BLOCK_SIZE * 2];
        IsoSize = GetIsoSize(out bool mismatch);
        IsPvdMismatch = mismatch;
    }

    private string GetDiscID()
    {
        var buffer = new byte[16];

        _stream.Seek(_psarOffset + PSAR_GAMEID_OFFSET, SeekOrigin.Begin);
        _stream.ReadByte();
        _stream.Read(buffer, 0, 4);
        _stream.ReadByte();
        _stream.Read(buffer, 4, 5);

        return System.Text.Encoding.ASCII.GetString(buffer, 0, 9);
    }

    private List<TocEntry> ReadTOC()
    {
        var entries = new List<TocEntry>();
        var buffer = new byte[0xA];

        _stream.Seek(_psarOffset + PSAR_TOC_OFFSET, SeekOrigin.Begin);
        _stream.Read(buffer, 0, 0xA);

        if (buffer[2] != 0xA0) 
            throw new Exception("Invalid TOC!");

        int startTrack = TocHelper.FromBinaryDecimal(buffer[7]);

        _stream.Read(buffer, 0, 0xA);

        if (buffer[2] != 0xA1) 
            throw new Exception("Invalid TOC!");

        int endTrack = TocHelper.FromBinaryDecimal(buffer[7]);

        _stream.Read(buffer, 0, 0xA);

        if (buffer[2] != 0xA2) 
            throw new Exception("Invalid TOC!");

        for (var c = startTrack; c <= endTrack; c++)
        {
            _stream.Read(buffer, 0, 0xA);

            var trackNo = TocHelper.FromBinaryDecimal(buffer[2]);

            if (trackNo != c) 
                throw new Exception("Invalid TOC!");

            entries.Add(new TocEntry
            {
                TrackType = (TrackType)buffer[0],
                TrackNo = trackNo,
                Minutes = TocHelper.FromBinaryDecimal(buffer[3]),
                Seconds = TocHelper.FromBinaryDecimal(buffer[4]),
                Frames = TocHelper.FromBinaryDecimal(buffer[5])
            });
        }

        return entries;
    }

    private List<IsoBlock> ReadIsoIndexes()
    {
        var isoIndex = new List<IsoBlock>();
        var dummy = new int[6];

        _stream.Seek(_psarOffset + PSAR_INDEX_OFFSET, SeekOrigin.Begin);

        var thisOffset = (uint)_stream.Position;
        var count = 0;

        while (thisOffset < _psarOffset + PSAR_ISO_OFFSET)
        {
            var offset = (uint)_stream.ReadInteger();
            var length = _stream.ReadInteger();

            _stream.Read(dummy, 6);
            thisOffset = (uint)_stream.Position;

            if (offset != 0 || length != 0)
            {
                isoIndex.Add(new IsoBlock { Offset = offset, Length = length });

                if (++count >= MAX_INDEXES)
                    throw new Exception("Number of indexes exceeds maximum allowed");
            }
        }

        if (isoIndex.Count == 0)
            throw new Exception("No iso index was found.");

        return isoIndex;
    }

    public uint ReadBlock(int blockNo, byte[] buffer)
    {
        var thisOffset = _psarOffset + PSAR_ISO_OFFSET + IsoIndex[blockNo].Offset;
        var blockLength = IsoIndex[blockNo].Length;

        _stream.Seek(thisOffset, SeekOrigin.Begin);

        if (blockLength == 16 * ISO_BLOCK_SIZE)
        {
            _stream.Read(buffer, 0, 16 * ISO_BLOCK_SIZE);

            return 16 * ISO_BLOCK_SIZE;
        }
        else
        {
            _stream.Read(_compressionInBuffer, 0, blockLength);

            var decompressed = Compression.Decompress(_compressionInBuffer, 16 * ISO_BLOCK_SIZE);

            Array.Copy(decompressed, buffer, decompressed.Length);

            return (uint)decompressed.Length;
        }
    }

    private uint GetIsoSize(out bool mismatch)
    {
        var outBuffer = new byte[16 * ISO_BLOCK_SIZE];

        ReadBlock(1, outBuffer);

        var pvdSectors = (uint)(outBuffer[104] + (outBuffer[105] << 8) + (outBuffer[106] << 16) + (outBuffer[107] << 24));
        var pvdSize = pvdSectors * ISO_BLOCK_SIZE;
        var maxSize = (uint)(IsoIndex.Count * 16 * ISO_BLOCK_SIZE);
        const uint blockTolerance = 16 * ISO_BLOCK_SIZE;

        var diff = pvdSize > maxSize ? pvdSize - maxSize : maxSize - pvdSize;

        mismatch = pvdSize == 0 || diff > blockTolerance;

        return mismatch ? maxSize : pvdSize;
    }

    public long EndOffset
    {
        get
        {
            var lastBlock = IsoIndex[^1];

            return _psarOffset + PSAR_ISO_OFFSET + lastBlock.Offset + lastBlock.Length;
        }
    }

    public PbpDiscStream GetDiscStream() => new(this);

    public void CopyTo(Stream destination, CancellationToken cancellationToken)
    {
        uint totSize = 0;
        var outBuffer = new byte[16 * ISO_BLOCK_SIZE];

        for (var i = 0; i < IsoIndex.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bufferSize = ReadBlock(i, outBuffer);
            totSize += bufferSize;

            if (totSize > IsoSize)
            {
                bufferSize -= totSize - IsoSize;
                totSize = IsoSize;
            }

            destination.Write(outBuffer, 0, (int)bufferSize);
            ProgressEvent?.Invoke(totSize);
        }
    }

    public void Dispose()
    {
        _stream?.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_stream != null) 
            await _stream.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}