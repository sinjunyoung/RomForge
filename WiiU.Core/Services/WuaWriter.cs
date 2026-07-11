// WuaWriter.cs
//
// C# port of Exzap's ZArchive writer (https://github.com/Exzap/ZArchive, MIT license).
// A .wua (Wii U Archive) file is simply a ZArchive (.zar) file — Cemu just adds a naming
// convention on top ("16-digit-titleId_vVERSION" subfolder per title). This class implements
// the .zar container format itself: 64KiB zstd-compressed blocks, a BFS-serialized file tree,
// a length-prefixed name table, and a footer with per-section offsets + a SHA-256 integrity hash.
//
// Dependency: ZstdSharp.Port (NuGet) — pure managed zstd implementation, no native binary,
// no external process invocation.
//
//   dotnet add package ZstdSharp.Port
//
// Usage:
//   await using var fs = File.Create(@"D:\out\MyGame.wua");
//   using var writer = new WuaWriter(fs);
//   writer.MakeDir("0005000e10102000_v32/code", recursive: true);
//   writer.MakeDir("0005000e10102000_v32/content", recursive: true);
//   writer.MakeDir("0005000e10102000_v32/meta", recursive: true);
//   writer.StartNewFile("0005000e10102000_v32/code/main.rpx");
//   writer.AppendData(rpxBytes);
//   // ... repeat StartNewFile/AppendData for every file in the title dump ...
//   writer.FinalizeArchive();

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using ZstdSharp;

namespace WiiU.Core.Services;

public sealed class WuaWriter : IDisposable
{
    private const int CompressedBlockSize = 64 * 1024; // 64 KiB
    private const int EntriesPerOffsetRecord = 16;
    private const uint FooterMagic = 0x169f52d6;
    private const uint FooterVersion1 = 0x61bf3a01;
    private const int OffsetRecordSize = 8 + 2 * EntriesPerOffsetRecord; // 40 bytes
    private const int FileDirectoryEntrySize = 16;
    private const int FooterSize = 16 * 6 + 32 + 8 + 4 + 4; // 156 bytes

    private sealed class PathNode
    {
        public bool IsFile;
        public uint NameIndex = uint.MaxValue;
        public readonly List<PathNode> Subnodes = new();

        // file
        public ulong FileOffset;
        public ulong FileSize;
        // directory
        public uint NodeStartIndex;
    }

    private sealed class OffsetRecord
    {
        public ulong BaseOffset;
        public readonly ushort[] Size = new ushort[EntriesPerOffsetRecord];
    }

    private readonly Stream _output;
    private readonly PathNode _root = new() { IsFile = false };
    private PathNode? _currentFileNode;

    private readonly List<string> _nodeNames = new();
    private readonly Dictionary<string, uint> _nodeNameLookup = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<uint> _nodeNameOffsets = new();

    private readonly List<OffsetRecord> _offsetRecords = new();
    private ulong _numWrittenOffsetRecords;

    private byte[] _writeBuffer = new byte[CompressedBlockSize];
    private int _writeBufferLength;

    private ulong _outputOffset;   // bytes written to output so far (== compressed write index)
    private ulong _inputOffset;    // uncompressed bytes appended so far

    private readonly Compressor _zstdCompressor = new(level: 6);
    private readonly IncrementalHash _sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    // footer section offsets/sizes
    private (ulong Offset, ulong Size) _secCompressedData;
    private (ulong Offset, ulong Size) _secOffsetRecords;
    private (ulong Offset, ulong Size) _secNames;
    private (ulong Offset, ulong Size) _secFileTree;
    private (ulong Offset, ulong Size) _secMetaDirectory;
    private (ulong Offset, ulong Size) _secMetaData;

    private bool _finalized;

    public WuaWriter(Stream output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));
    }

    // ---------------------------------------------------------------
    // Public API — mirrors ZArchiveWriter's StartNewFile/AppendData/MakeDir/Finalize
    // ---------------------------------------------------------------

    public bool MakeDir(string path, bool recursive = false)
    {
        path = path.Trim('/', '\\');
        if (!recursive)
        {
            SplitFilenameFromPath(path, out var parentPath, out var dirName);
            var dir = GetNodeByPath(parentPath);
            if (dir is null) return false;
            if (FindSubnodeByName(dir, dirName) is not null) return false;
            dir.Subnodes.Add(new PathNode { IsFile = false, NameIndex = CreateNameEntry(dirName) });
            return true;
        }

        var current = _root;
        foreach (var node in SplitPath(path))
        {
            var next = FindSubnodeByName(current, node);
            if (next is { IsFile: true }) return false;
            if (next is null)
            {
                next = new PathNode { IsFile = false, NameIndex = CreateNameEntry(node) };
                current.Subnodes.Add(next);
            }
            current = next;
        }
        return true;
    }

    public bool StartNewFile(string path)
    {
        _currentFileNode = null;
        path = path.Trim('/', '\\');
        SplitFilenameFromPath(path, out var parentPath, out var filename);
        var dir = GetNodeByPath(parentPath);
        if (dir is null) return false;
        if (FindSubnodeByName(dir, filename) is not null) return false;

        var node = new PathNode { IsFile = true, NameIndex = CreateNameEntry(filename), FileOffset = _inputOffset };
        dir.Subnodes.Add(node);
        _currentFileNode = node;
        return true;
    }

    public void AppendData(ReadOnlySpan<byte> data)
    {
        var dataLen = data.Length;
        while (!data.IsEmpty)
        {
            var bytesToCopy = Math.Min(CompressedBlockSize - _writeBufferLength, data.Length);
            if (bytesToCopy == CompressedBlockSize)
            {
                // block-aligned, store directly without buffering
                StoreBlock(data[..CompressedBlockSize]);
                data = data[CompressedBlockSize..];
                continue;
            }
            data[..bytesToCopy].CopyTo(_writeBuffer.AsSpan(_writeBufferLength));
            _writeBufferLength += bytesToCopy;
            data = data[bytesToCopy..];
            if (_writeBufferLength == CompressedBlockSize)
            {
                StoreBlock(_writeBuffer);
                _writeBufferLength = 0;
            }
        }

        if (_currentFileNode is not null)
            _currentFileNode.FileSize += (ulong)dataLen;
        _inputOffset += (ulong)dataLen;
    }

    public void FinalizeArchive()
    {
        if (_finalized) throw new InvalidOperationException("Already finalized.");
        _finalized = true;

        _currentFileNode = null; // padding below must not count towards a file's size
        if (_writeBufferLength > 0)
        {
            var pad = new byte[CompressedBlockSize - _writeBufferLength];
            AppendData(pad);
        }

        _secCompressedData = (0, _outputOffset);

        // pad to 8-byte boundary
        Span<byte> zero = stackalloc byte[1];
        while ((_outputOffset % 8) != 0)
            OutputData(zero);

        WriteOffsetRecords();
        WriteNameTable();
        WriteFileTree();
        WriteMetaData();
        WriteFooter();
    }

    public void Dispose()
    {
        _zstdCompressor.Dispose();
        _sha256.Dispose();
    }

    // ---------------------------------------------------------------
    // Internal helpers
    // ---------------------------------------------------------------

    private void OutputData(ReadOnlySpan<byte> data)
    {
        _output.Write(data);
        _outputOffset += (ulong)data.Length;
        _sha256.AppendData(data);
    }

    private void StoreBlock(ReadOnlySpan<byte> uncompressedBlock)
    {
        // uncompressedBlock is always exactly CompressedBlockSize bytes here
        var compressedWriteOffset = _outputOffset;
        var compressed = _zstdCompressor.Wrap(uncompressedBlock);

        int outputSize;
        if (compressed.Length >= CompressedBlockSize)
        {
            // store raw if compression didn't help
            outputSize = CompressedBlockSize;
            OutputData(uncompressedBlock);
        }
        else
        {
            outputSize = compressed.Length;
            OutputData(compressed);
        }

        if ((_numWrittenOffsetRecords % EntriesPerOffsetRecord) == 0)
            _offsetRecords.Add(new OffsetRecord { BaseOffset = compressedWriteOffset });

        var rec = _offsetRecords[^1];
        rec.Size[_numWrittenOffsetRecords % EntriesPerOffsetRecord] = (ushort)(outputSize - 1);
        _numWrittenOffsetRecords++;
    }

    private void WriteOffsetRecords()
    {
        _secOffsetRecords = (_outputOffset, 0);
        Span<byte> buf = stackalloc byte[OffsetRecordSize];
        foreach (var rec in _offsetRecords)
        {
            BinaryPrimitives.WriteUInt64BigEndian(buf, rec.BaseOffset);
            for (int i = 0; i < EntriesPerOffsetRecord; i++)
                BinaryPrimitives.WriteUInt16BigEndian(buf.Slice(8 + i * 2, 2), rec.Size[i]);
            OutputData(buf);
        }
        _secOffsetRecords.Size = _outputOffset - _secOffsetRecords.Offset;
    }

    private void WriteNameTable()
    {
        _secNames = (_outputOffset, 0);
        _nodeNameOffsets.Clear();
        uint currentOffset = 0;
        Span<byte> header = stackalloc byte[2];
        foreach (var name in _nodeNames)
        {
            _nodeNameOffsets.Add(currentOffset);
            var bytes = Encoding.UTF8.GetBytes(name.Length > 0x7FFF ? name[..0x7FFF] : name);
            if (bytes.Length >= 0x80)
            {
                header[0] = (byte)((bytes.Length & 0x7F) | 0x80);
                header[1] = (byte)(bytes.Length >> 7);
                OutputData(header);
                currentOffset += 2;
            }
            else
            {
                header[0] = (byte)(bytes.Length & 0x7F);
                OutputData(header[..1]);
                currentOffset += 1;
            }
            OutputData(bytes);
            currentOffset += (uint)bytes.Length;
        }
        _secNames.Size = _outputOffset - _secNames.Offset;
    }

    private void WriteFileTree()
    {
        // first pass: BFS to assign node index ranges to directories
        var queue = new Queue<PathNode>();
        queue.Enqueue(_root);
        uint currentIndex = 1; // root occupies index 0
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (node.IsFile)
            {
                node.NodeStartIndex = 0xFFFFFFFF;
                continue;
            }
            // sort children ascending, case-insensitive (matches CompareNodeName ordering)
            node.Subnodes.Sort((a, b) =>
                string.Compare(_nodeNames[(int)a.NameIndex], _nodeNames[(int)b.NameIndex], StringComparison.OrdinalIgnoreCase));

            node.NodeStartIndex = currentIndex;
            currentIndex += (uint)node.Subnodes.Count;
            foreach (var sub in node.Subnodes)
                queue.Enqueue(sub);
        }

        // second pass: serialize BFS order
        _secFileTree = (_outputOffset, 0);
        queue.Enqueue(_root);
        Span<byte> entry = stackalloc byte[FileDirectoryEntrySize];
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            uint nameOffsetAndTypeFlag = node == _root ? 0x7FFFFFFF : _nodeNameOffsets[(int)node.NameIndex];
            if (node.IsFile) nameOffsetAndTypeFlag |= 0x80000000;

            entry.Clear();
            BinaryPrimitives.WriteUInt32BigEndian(entry[..4], nameOffsetAndTypeFlag);
            if (node.IsFile)
            {
                var fileOffsetLow = (uint)node.FileOffset;
                var fileSizeLow = (uint)node.FileSize;
                var high = (uint)((node.FileSize >> 16) & 0xFFFF0000) | (uint)((node.FileOffset >> 32) & 0xFFFF);
                BinaryPrimitives.WriteUInt32BigEndian(entry[4..8], fileOffsetLow);
                BinaryPrimitives.WriteUInt32BigEndian(entry[8..12], fileSizeLow);
                BinaryPrimitives.WriteUInt32BigEndian(entry[12..16], high);
            }
            else
            {
                BinaryPrimitives.WriteUInt32BigEndian(entry[4..8], node.NodeStartIndex);
                BinaryPrimitives.WriteUInt32BigEndian(entry[8..12], (uint)node.Subnodes.Count);
                BinaryPrimitives.WriteUInt32BigEndian(entry[12..16], 0);
            }
            OutputData(entry);
            foreach (var sub in node.Subnodes)
                queue.Enqueue(sub);
        }
        _secFileTree.Size = _outputOffset - _secFileTree.Offset;
    }

    private void WriteMetaData()
    {
        // Not implemented upstream either ("todo" in ZArchive itself) — zero-length sections.
        _secMetaDirectory = (_outputOffset, 0);
        _secMetaData = (_outputOffset, 0);
    }

    private void WriteFooter()
    {
        var totalSize = _outputOffset + (ulong)FooterSize;
        Span<byte> footer = stackalloc byte[FooterSize];

        void WriteSection(Span<byte> dest, (ulong Offset, ulong Size) sec)
        {
            BinaryPrimitives.WriteUInt64BigEndian(dest[..8], sec.Offset);
            BinaryPrimitives.WriteUInt64BigEndian(dest[8..16], sec.Size);
        }

        WriteSection(footer[0..16], _secCompressedData);
        WriteSection(footer[16..32], _secOffsetRecords);
        WriteSection(footer[32..48], _secNames);
        WriteSection(footer[48..64], _secFileTree);
        WriteSection(footer[64..80], _secMetaDirectory);
        WriteSection(footer[80..96], _secMetaData);
        // bytes [96..128) = integrityHash, left as zero for hashing pass
        BinaryPrimitives.WriteUInt64BigEndian(footer[128..136], totalSize);
        BinaryPrimitives.WriteUInt32BigEndian(footer[136..140], FooterVersion1);
        BinaryPrimitives.WriteUInt32BigEndian(footer[140..144], FooterMagic);
        // footer struct is 156 bytes total (16*6 + 32 + 8 + 4 + 4); indices above cover the
        // first 144 bytes (6 sections @16 + hash-placeholder handled separately below) —
        // hash occupies [96:128), already zeroed by stackalloc.

        // hash everything written so far + this footer (with hash field zeroed), then re-emit
        // the footer with the real hash. This matches upstream: the hash covers the whole file
        // including the footer, with the hash field itself treated as zero during hashing.
        _sha256.AppendData(footer);
        var hash = _sha256.GetHashAndReset();
        hash.CopyTo(footer[96..128]);

        _output.Write(footer);
        _outputOffset += (ulong)footer.Length;
    }

    private uint CreateNameEntry(string name)
    {
        if (_nodeNameLookup.TryGetValue(name, out var existing))
            return existing;
        var index = (uint)_nodeNames.Count;
        _nodeNames.Add(name);
        _nodeNameLookup[name] = index;
        return index;
    }

    private PathNode? GetNodeByPath(string path)
    {
        var current = _root;
        foreach (var part in SplitPath(path))
        {
            var next = FindSubnodeByName(current, part);
            if (next is null || next.IsFile) return null;
            current = next;
        }
        return current;
    }

    private PathNode? FindSubnodeByName(PathNode parent, string name)
    {
        foreach (var sub in parent.Subnodes)
            if (string.Equals(_nodeNames[(int)sub.NameIndex], name, StringComparison.OrdinalIgnoreCase))
                return sub;
        return null;
    }

    private static IEnumerable<string> SplitPath(string path)
    {
        foreach (var part in path.Split('/', '\\', StringSplitOptions.RemoveEmptyEntries))
            yield return part;
    }

    private static void SplitFilenameFromPath(string path, out string parentPath, out string filename)
    {
        var idx = path.LastIndexOfAny(['/', '\\']);
        if (idx < 0)
        {
            parentPath = string.Empty;
            filename = path;
        }
        else
        {
            parentPath = path[..idx];
            filename = path[(idx + 1)..];
        }
    }
}
