
using System.Buffers.Binary;
using System.Text;
using WiiU.Core.Models;
using ZstdSharp;

namespace WiiU.Core.Services;

public sealed class WuaReader : IDisposable
{
    public const uint RootNode = 0;
    public const uint InvalidNodeHandle = InvalidNode;

    private const int CompressedBlockSize = 64 * 1024;
    private const int EntriesPerOffsetRecord = 16;
    private const uint FooterMagic = 0x169f52d6;
    private const uint FooterVersion1 = 0x61bf3a01;
    private const int FooterSize = 16 * 6 + 32 + 8 + 4 + 4;
    private const int FileDirectoryEntrySize = 16;
    private const uint InvalidNode = 0xFFFFFFFF;

    private sealed class OffsetRecord
    {
        public ulong BaseOffset;
        public readonly ushort[] Size = new ushort[EntriesPerOffsetRecord];
    }

    private sealed class FileDirectoryEntry
    {
        public bool IsFile;
        public uint NameOffset;
        public ulong FileOffset;
        public ulong FileSize;
        public uint NodeStartIndex;
        public uint Count;
    }

    private readonly Stream _stream;
    private readonly List<OffsetRecord> _offsetRecords = [];
    private byte[] _nameTable = [];
    private readonly List<FileDirectoryEntry> _fileTree = [];
    private ulong _compressedDataOffset;
    private ulong _compressedDataSize;
    private readonly Decompressor _zstd = new();

    private WuaReader(Stream stream)
    {
        _stream = stream;
    }

    public static WuaReader OpenFromFile(string path)
    {
        var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);

        try
        {
            return OpenFromStream(fs);
        }
        catch
        {
            fs.Dispose();
            throw;
        }
    }

    public static WuaReader OpenFromStream(Stream stream)
    {
        long fileSize = stream.Length;

        if (fileSize <= FooterSize)
            throw new InvalidDataException("File is too small to contain a valid footer.");

        var footer = new byte[FooterSize];

        stream.Position = fileSize - FooterSize;

        if (stream.Read(footer, 0, FooterSize) != FooterSize)
            throw new InvalidDataException("Could not read footer.");

        var sections = new (ulong Offset, ulong Size)[6];

        for (int i = 0; i < 6; i++)
            sections[i] = (BinaryPrimitives.ReadUInt64BigEndian(footer.AsSpan(i * 16, 8)), BinaryPrimitives.ReadUInt64BigEndian(footer.AsSpan(i * 16 + 8, 8)));

        ulong totalSize = BinaryPrimitives.ReadUInt64BigEndian(footer.AsSpan(128, 8));
        uint version = BinaryPrimitives.ReadUInt32BigEndian(footer.AsSpan(136, 4));
        uint magic = BinaryPrimitives.ReadUInt32BigEndian(footer.AsSpan(140, 4));

        if (magic != FooterMagic) 
            throw new InvalidDataException("Not a .wua/.zar file (magic mismatch).");

        if (version != FooterVersion1) 
            throw new InvalidDataException("Unsupported .wua/.zar version.");

        if ((long)totalSize != fileSize) 
            throw new InvalidDataException("Footer totalSize does not match the actual file size.");

        foreach (var (offset, size) in sections)
        {
            if (offset + size > totalSize)
                throw new InvalidDataException("A footer section falls outside the file — corrupt archive.");
        }

        var reader = new WuaReader(stream)
        {
            _compressedDataOffset = sections[0].Offset,
            _compressedDataSize = sections[0].Size,
        };

        var (recOffset, recSize) = sections[1];

        if (recSize % 40 != 0) 
            throw new InvalidDataException("Offset record section size is invalid.");

        int recordCount = (int)(recSize / 40);

        stream.Position = (long)recOffset;

        for (int i = 0; i < recordCount; i++)
        {
            var buf = new byte[40];

            if (stream.Read(buf, 0, 40) != 40) 
                throw new InvalidDataException("Could not read offset records.");

            var rec = new OffsetRecord { BaseOffset = BinaryPrimitives.ReadUInt64BigEndian(buf.AsSpan(0, 8)) };

            for (int j = 0; j < EntriesPerOffsetRecord; j++)
                rec.Size[j] = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(8 + j * 2, 2));

            reader._offsetRecords.Add(rec);
        }

        var (nameOffset, nameSize) = sections[2];

        if (nameSize > 0x7FFFFFFF) 
            throw new InvalidDataException("Name table too large.");

        reader._nameTable = new byte[nameSize];
        stream.Position = (long)nameOffset;

        if (stream.Read(reader._nameTable, 0, (int)nameSize) != (int)nameSize)
            throw new InvalidDataException("Could not read name table.");

        var (treeOffset, treeSize) = sections[3];

        if (treeSize % FileDirectoryEntrySize != 0) 
            throw new InvalidDataException("File tree section size is invalid.");

        int nodeCount = (int)(treeSize / FileDirectoryEntrySize);

        if (nodeCount == 0) 
            throw new InvalidDataException("File tree is empty.");

        stream.Position = (long)treeOffset;

        for (int i = 0; i < nodeCount; i++)
        {
            var buf = new byte[FileDirectoryEntrySize];

            if (stream.Read(buf, 0, FileDirectoryEntrySize) != FileDirectoryEntrySize)
                throw new InvalidDataException("Could not read file tree.");

            uint field0 = BinaryPrimitives.ReadUInt32BigEndian(buf.AsSpan(0, 4));
            bool isFile = (field0 & 0x80000000) != 0;
            uint nameOff = field0 & 0x7FFFFFFF;
            var entry = new FileDirectoryEntry { IsFile = isFile, NameOffset = nameOff };

            if (isFile)
            {
                uint fileOffsetLow = BinaryPrimitives.ReadUInt32BigEndian(buf.AsSpan(4, 4));
                uint fileSizeLow = BinaryPrimitives.ReadUInt32BigEndian(buf.AsSpan(8, 4));
                uint high = BinaryPrimitives.ReadUInt32BigEndian(buf.AsSpan(12, 4));

                entry.FileOffset = fileOffsetLow | ((ulong)(high & 0xFFFF) << 32);
                entry.FileSize = fileSizeLow | ((ulong)((high >> 16) & 0xFFFF) << 32);
            }
            else
            {
                entry.NodeStartIndex = BinaryPrimitives.ReadUInt32BigEndian(buf.AsSpan(4, 4));
                entry.Count = BinaryPrimitives.ReadUInt32BigEndian(buf.AsSpan(8, 4));
            }

            reader._fileTree.Add(entry);
        }

        if (reader._fileTree[0].IsFile)
            throw new InvalidDataException("First file tree entry must be the root directory.");

        if (GetName(reader._nameTable, reader._fileTree[0].NameOffset).Length != 0)
            throw new InvalidDataException("Root node must not have a name.");

        return reader;
    }

    public uint LookUp(string path)
    {
        uint currentNode = 0;

        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var entry = _fileTree[(int)currentNode];

            if (entry.IsFile) 
                return InvalidNode;

            uint match = InvalidNode;

            for (uint i = entry.NodeStartIndex; i < entry.NodeStartIndex + entry.Count; i++)
            {
                string name = GetName(_nameTable, _fileTree[(int)i].NameOffset);

                if (string.Equals(name, part, StringComparison.Ordinal))
                {
                    match = i;
                    break;
                }
            }
            if (match == InvalidNode) 
                return InvalidNode;

            currentNode = match;
        }

        return currentNode;
    }

    public bool IsFile(uint node) => node < _fileTree.Count && _fileTree[(int)node].IsFile;

    public bool IsDirectory(uint node) => node < _fileTree.Count && !_fileTree[(int)node].IsFile;

    public ulong GetFileSize(uint node) => node < _fileTree.Count && _fileTree[(int)node].IsFile ? _fileTree[(int)node].FileSize : 0;

    public uint GetDirEntryCount(uint node)
        => node < _fileTree.Count && !_fileTree[(int)node].IsFile ? _fileTree[(int)node].Count : 0;

    public bool GetDirEntry(uint node, uint index, out WuaDirEntry dirEntry)
    {
        dirEntry = new WuaDirEntry();

        if (node >= _fileTree.Count) 
            return false;

        var dir = _fileTree[(int)node];

        if (dir.IsFile || index >= dir.Count) 
            return false;

        var it = _fileTree[(int)(dir.NodeStartIndex + index)];

        dirEntry.IsFile = it.IsFile;
        dirEntry.Size = it.IsFile ? it.FileSize : 0;
        dirEntry.Name = GetName(_nameTable, it.NameOffset);

        return dirEntry.Name.Length > 0 || dir.NodeStartIndex + index == 0;
    }

    public IEnumerable<(string Path, uint Node)> EnumerateFiles() => EnumerateFiles(RootNode, string.Empty);

    private IEnumerable<(string, uint)> EnumerateFiles(uint dirNode, string prefix)
    {
        var dir = _fileTree[(int)dirNode];

        for (uint i = dir.NodeStartIndex; i < dir.NodeStartIndex + dir.Count; i++)
        {
            var entry = _fileTree[(int)i];
            string name = GetName(_nameTable, entry.NameOffset);
            string path = prefix.Length == 0 ? name : $"{prefix}/{name}";

            if (entry.IsFile)
            {
                yield return (path, i);
            }
            else
            {
                foreach (var sub in EnumerateFiles(i, path))
                    yield return sub;
            }
        }
    }

    public Stream OpenFileStream(uint node)
    {
        if (!IsFile(node))
            throw new InvalidOperationException("Cannot open a directory as a file stream.");

        long size = (long)GetFileSize(node);

        return new DelegateReadStream(size, (pos, buf, off, cnt) => ReadFromFile(node, (ulong)pos, buf, off, cnt));
    }

    public int ReadFromFile(uint node, ulong offset, byte[] buffer, int bufferOffset, int length)
    {
        if (node >= _fileTree.Count)
            return 0;

        var file = _fileTree[(int)node];

        if (!file.IsFile) 
            return 0;

        if (offset >= file.FileSize) 
            return 0;

        ulong bytesToRead = Math.Min((ulong)length, file.FileSize - offset);
        ulong rawReadOffset = file.FileOffset + offset;
        ulong remaining = bytesToRead;
        int written = 0;

        while (remaining > 0)
        {
            ulong blockIdx = rawReadOffset / CompressedBlockSize;
            int blockOffset = (int)(rawReadOffset % CompressedBlockSize);
            int stepSize = (int)Math.Min(remaining, (ulong)(CompressedBlockSize - blockOffset));

            byte[] block = LoadBlock(blockIdx);
            Array.Copy(block, blockOffset, buffer, bufferOffset + written, stepSize);

            rawReadOffset += (ulong)stepSize;
            remaining -= (ulong)stepSize;
            written += stepSize;
        }

        return written;
    }

    public void ExtractFileTo(uint node, Stream destination)
    {
        ulong size = GetFileSize(node);
        var buffer = new byte[Math.Min(1024 * 1024UL, size == 0 ? 1 : size)];
        ulong offset = 0;

        while (offset < size)
        {
            int toRead = (int)Math.Min((ulong)buffer.Length, size - offset);
            int got = ReadFromFile(node, offset, buffer, 0, toRead);

            if (got <= 0) 
                break;

            destination.Write(buffer, 0, got);
            offset += (ulong)got;
        }
    }

    public byte[] ExtractFile(uint node)
    {
        var buffer = new byte[GetFileSize(node)];
        int totalRead = 0;

        while ((ulong)totalRead < (ulong)buffer.Length)
        {
            int got = ReadFromFile(node, (ulong)totalRead, buffer, totalRead, buffer.Length - totalRead);

            if (got <= 0) 
                break;

            totalRead += got;
        }
        return buffer;
    }

    private byte[] LoadBlock(ulong blockIndex)
    {
        uint recordIndex = (uint)(blockIndex / EntriesPerOffsetRecord);
        uint recordSubIndex = (uint)(blockIndex % EntriesPerOffsetRecord);

        if (recordIndex >= _offsetRecords.Count)
            throw new InvalidDataException("Block index out of range — corrupt archive.");

        var record = _offsetRecords[(int)recordIndex];
        ulong offset = record.BaseOffset;

        for (uint i = 0; i < recordSubIndex; i++)
        {
            offset += record.Size[i];
            offset++;
        }

        uint compressedSize = (uint)record.Size[recordSubIndex] + 1;

        if (offset + compressedSize > _compressedDataSize)
            throw new InvalidDataException("Compressed block extends past the compressed data section — corrupt archive.");

        offset += _compressedDataOffset;

        var output = new byte[CompressedBlockSize];

        _stream.Position = (long)offset;

        if (compressedSize == CompressedBlockSize)
        {
            if (_stream.Read(output, 0, CompressedBlockSize) != CompressedBlockSize)
                throw new InvalidDataException("Failed to read raw block.");

            return output;
        }

        var compressed = new byte[compressedSize];

        if (_stream.Read(compressed, 0, (int)compressedSize) != compressedSize)
            throw new InvalidDataException("Failed to read compressed block.");

        var decompressed = _zstd.Unwrap(compressed);

        if (decompressed.Length != CompressedBlockSize)
            throw new InvalidDataException("Decompressed block has unexpected size — corrupt archive.");

        decompressed.CopyTo(output);

        return output;
    }

    private static string GetName(byte[] nameTable, uint nameOffset)
    {
        if (nameOffset == 0x7FFFFFFF || nameOffset >= nameTable.Length) 
            return string.Empty;

        int pos = (int)nameOffset;
        int nameLength = nameTable[pos] & 0x7F;

        if ((nameTable[pos] & 0x80) != 0)
        {
            if (pos + 1 >= nameTable.Length) 
                return string.Empty;

            nameLength |= nameTable[pos] << 7;
            pos += 2;
        }
        else
            pos += 1;

        if (pos + nameLength > nameTable.Length) 
            return string.Empty;

        return Encoding.UTF8.GetString(nameTable, pos, nameLength);
    }

    public void Dispose()
    {
        _zstd.Dispose();
        _stream.Dispose();
    }
}