// WudReader.cs
//
// C# port of cemu-project/WudCompress (wud.h / wud.cpp).
// Transparently reads both .wud (raw Wii U disc image) and .wux (sector-deduplicated,
// NOT compressed — despite the name, WUX uses no zlib/zstd, just an index table that lets
// identical sectors share one physical copy) as a single flat byte stream.
//
// Note: unlike the ZArchive/.wua format, the .wud/.wux header and index table are stored
// in NATIVE LITTLE-ENDIAN, matching the original Windows/MSVC tool.

using System;
using System.Buffers.Binary;
using System.IO;

namespace WiiU.Core.Services;

public sealed class WudReader : IDisposable
{
    private const uint WuxMagic0 = 0x30585557; // "WUX0" as little-endian uint32
    private const uint WuxMagic1 = 0x1099d02e;
    private const int HeaderSize = 32; // sizeof(wuxHeader_t) with natural struct alignment

    private readonly Stream _stream;
    private readonly bool _leaveOpen;

    public bool IsCompressed { get; private set; }
    public long UncompressedSize { get; private set; }

    private uint _sectorSize;
    private uint[] _indexTable = Array.Empty<uint>();
    private long _offsetIndexTable;
    private long _offsetSectorArray;

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
            _offsetIndexTable = _stream.Position; // right after header, matches wud_getCurrentSeek64
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

    /// <summary>
    /// Reads up to <paramref name="buffer"/>.Length bytes starting at logical <paramref name="offset"/>
    /// into the uncompressed WUD data stream. Returns the number of bytes actually read
    /// (fewer than requested only at end-of-file).
    /// </summary>
    public int ReadData(Span<byte> buffer, long offset)
    {
        long fileBytesLeft = UncompressedSize - offset;
        if (fileBytesLeft <= 0) return 0;
        int length = buffer.Length;
        if (fileBytesLeft < length) length = (int)fileBytesLeft;

        int totalRead = 0;

        if (!IsCompressed)
        {
            _stream.Position = offset;
            totalRead = _stream.ReadAtLeast(buffer[..length], length, throwOnEndOfStream: false);
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
            int got = _stream.ReadAtLeast(dest[..bytesToRead], bytesToRead, throwOnEndOfStream: false);
            totalRead += got;

            dest = dest[bytesToRead..];
            remaining -= bytesToRead;
            offset += bytesToRead;

            if (got != bytesToRead) break; // unexpected EOF mid-sector
        }
        return totalRead;
    }

    /// <summary>Reads the full uncompressed WUD contents in <paramref name="chunkSize"/>-sized pieces,
    /// invoking <paramref name="onChunk"/> for each. Useful for streaming the disc image out
    /// to a decryption/parsing pass without buffering the whole thing in memory.</summary>
    public void ReadAll(Action<ReadOnlyMemory<byte>, long> onChunk, int chunkSize = 1 * 1024 * 1024)
    {
        var buffer = new byte[chunkSize];
        long offset = 0;
        while (offset < UncompressedSize)
        {
            int got = ReadData(buffer, offset);
            if (got <= 0) break;
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
