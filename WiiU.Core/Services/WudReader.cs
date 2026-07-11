using System.Buffers.Binary;

namespace WiiU.Core.Services;

public sealed class WudReader : IDisposable
{
    private const uint WuxMagic0 = 0x30585557;
    private const uint WuxMagic1 = 0x1099d02e;
    private const int HeaderSize = 32;

    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private uint _sectorSize;
    private uint[] _indexTable = [];
    private long _offsetIndexTable;
    private long _offsetSectorArray;

    public bool IsCompressed { get; private set; }

    public long UncompressedSize { get; private set; }

    private WudReader(Stream stream, bool leaveOpen)
    {
        _stream = stream;
        _leaveOpen = leaveOpen;
    }

    public static WudReader Open(string path)
    {
        var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        try
        {
            return Open(fs, leaveOpen: false);
        }
        catch
        {
            fs.Dispose();
            throw;
        }
    }

    public static WudReader Open(Stream stream, bool leaveOpen)
    {
        var reader = new WudReader(stream, leaveOpen);
        reader.Initialize();

        return reader;
    }

    private void Initialize()
    {
        long inputFileSize = _stream.Length;

        Span<byte> header = stackalloc byte[HeaderSize];

        _stream.Position = 0;

        int read = _stream.ReadAtLeast(header, HeaderSize, throwOnEndOfStream: false);

        if (read != HeaderSize)
            throw new InvalidDataException("File is too short to be a valid .wud/.wux.");

        uint magic0 = BinaryPrimitives.ReadUInt32LittleEndian(header[0..4]);
        uint magic1 = BinaryPrimitives.ReadUInt32LittleEndian(header[4..8]);

        if (magic0 == WuxMagic0 && magic1 == WuxMagic1)
        {
            IsCompressed = true;
            _sectorSize = BinaryPrimitives.ReadUInt32LittleEndian(header[8..12]);
            UncompressedSize = (long)BinaryPrimitives.ReadUInt64LittleEndian(header[16..24]);

            if (_sectorSize < 0x100 || _sectorSize >= 0x10000000)
                throw new InvalidDataException("Invalid .wux sector size.");

            uint indexTableEntryCount = (uint)((UncompressedSize + (_sectorSize - 1)) / _sectorSize);

            _offsetIndexTable = _stream.Position;

            long offsetSectorArray = _offsetIndexTable + (long)indexTableEntryCount * sizeof(uint);

            offsetSectorArray += _sectorSize - 1;
            offsetSectorArray -= offsetSectorArray % _sectorSize;
            _offsetSectorArray = offsetSectorArray;

            _indexTable = new uint[indexTableEntryCount];

            var indexBytes = new byte[indexTableEntryCount * sizeof(uint)];

            _stream.Position = _offsetIndexTable;

            int gotBytes = _stream.ReadAtLeast(indexBytes, indexBytes.Length, throwOnEndOfStream: false);

            if (gotBytes != indexBytes.Length)
                throw new InvalidDataException("Could not read the full .wux index table.");

            for (int i = 0; i < indexTableEntryCount; i++)
                _indexTable[i] = BinaryPrimitives.ReadUInt32LittleEndian(indexBytes.AsSpan(i * 4, 4));
        }
        else
        {
            IsCompressed = false;
            UncompressedSize = inputFileSize;
        }
    }

    public int ReadData(Span<byte> buffer, long offset)
    {
        long fileBytesLeft = UncompressedSize - offset;

        if (fileBytesLeft <= 0) 
            return 0;

        int length = buffer.Length;

        if (fileBytesLeft < length) 
            length = (int)fileBytesLeft;

        int totalRead = 0;

        if (!IsCompressed)
        {
            _stream.Position = offset;
            totalRead = _stream.ReadAtLeast(buffer[..length], length, false);

            return totalRead;
        }

        int remaining = length;
        var dest = buffer;

        while (remaining > 0)
        {
            uint sectorOffset = (uint)(offset % _sectorSize);
            uint remainingSectorBytes = _sectorSize - sectorOffset;
            uint logicalSectorIndex = (uint)(offset / _sectorSize);
            int bytesToRead = (int)Math.Min(remainingSectorBytes, (uint)remaining);
            uint physicalSectorIndex = _indexTable[logicalSectorIndex];
            long physicalPos = _offsetSectorArray + (long)physicalSectorIndex * _sectorSize + sectorOffset;

            _stream.Position = physicalPos;

            int got = _stream.ReadAtLeast(dest[..bytesToRead], bytesToRead, false);

            totalRead += got;
            dest = dest[bytesToRead..];
            remaining -= bytesToRead;
            offset += bytesToRead;

            if (got != bytesToRead)
                break;
        }
        return totalRead;
    }

    public void ReadAll(Action<ReadOnlyMemory<byte>, long> onChunk, int chunkSize = 1 * 1024 * 1024)
    {
        var buffer = new byte[chunkSize];
        long offset = 0;

        while (offset < UncompressedSize)
        {
            int got = ReadData(buffer, offset);

            if (got <= 0) 
                break;

            onChunk(buffer.AsMemory(0, got), offset);
            offset += got;
        }
    }

    public void Dispose()
    {
        if (!_leaveOpen)
            _stream.Dispose();
    }
}