using _3DS.Core.Interfaces;
using _3DS.Core.Models;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace _3DS.Core.Services;

public static class RomFsPacker
{
    private const int RomFsBlockSize = 0x1000;
    private const uint UnusedEntry = 0xFFFFFFFF;
    private const int Sha256Len = 0x20;
    private const int IvfcHeaderSize = 0x5C;
    private const int IvfcHeaderAlign = 0x10;
    private const int RomFsInfoSize = 0x28;
    private const int DirEntryFixed = 0x18;
    private const int FileEntryFixed = 0x20;

    public static async Task PackAsync(Stream ncchStream, RomFsUnpackResult unpack, Stream output, long totalBytes = 0, IRomFsFileSource? patchSource = null, Action<long, long>? progress = null, CancellationToken ct = default)
    {
        long dataBase = unpack.DataLevel2Offset + unpack.RomFsHeader.DataOffset;

        Dictionary<string, long>? patchSizeMap = null;

        if (patchSource != null)
            patchSizeMap = await BuildPatchSizeMapAsync(unpack.Files, patchSource, ncchStream, dataBase, ct);

        var (totalSize, _, _, _, _) = CalculateLayout(unpack.Directories, unpack.Files, patchSizeMap);
        long startPos = output.Position;

        if (output.CanSeek && output.Length < startPos + (long)totalSize)
            output.SetLength(startPos + (long)totalSize);

        await PackInternalAsync(ncchStream, dataBase, unpack.Directories, unpack.Files, output, startPos, totalBytes, patchSource, patchSizeMap, progress, ct);

    }

    public static async Task PackFromFolderAsync(string folderPath, Stream output, IRomFsFileSource? patchSource = null, Action<long, long>? progress = null, CancellationToken ct = default)
    {
        var (dirs, files) = ScanFolder(folderPath);

        IRomFsFileSource effectiveSource = new FolderRomFsFileSource(folderPath, patchSource);

        var patchSizeMap = await BuildPatchSizeMapAsync(files, effectiveSource, null, 0, ct);
        var (totalSize, _, _, _, _) = CalculateLayout(dirs, files, patchSizeMap);
        long startPos = output.Position;

        if (output.CanSeek && output.Length < startPos + (long)totalSize)
            output.SetLength(startPos + (long)totalSize);

        await PackInternalAsync(Stream.Null, 0, dirs, files, output, startPos, 0, effectiveSource, patchSizeMap, progress, ct);
    }

    public static (ulong totalSize, ulong level0Size, ulong offLevel3, ulong offLevel1Hash, ulong offLevel2Hash) CalculateLayout(IReadOnlyList<RomFsDirNode> dirs, IReadOnlyList<RomFsFileNode> files, Dictionary<string, long>? patchSizeMap = null)
    {
        uint dirHashCount = GetHashTableCount((uint)dirs.Count);
        uint fileHashCount = GetHashTableCount((uint)files.Count);
        uint dirTableLen = 0;

        foreach (var d in dirs)
        {
            uint nameBytes = (uint)Encoding.Unicode.GetByteCount(d.Entry.Name);
            dirTableLen += d.Entry.Name.Length == 0 ? (uint)DirEntryFixed : (uint)(DirEntryFixed + AlignUp(nameBytes, 4));
        }

        uint fileTableLen = 0;

        foreach (var f in files)
        {
            uint nameBytes = (uint)Encoding.Unicode.GetByteCount(f.Entry.Name);
            fileTableLen += FileEntryFixed + AlignUp(nameBytes, 4);
        }

        ulong dataLen = 0;

        foreach (var f in files)
        {
            long size = patchSizeMap?.TryGetValue(f.FullPath, out long ps) == true ? ps : (long)f.DataSize;
            if (size > 0)
                dataLen = AlignUp(dataLen, 0x10) + (ulong)size;
        }

        uint romfsHdrSize = AlignUp(RomFsInfoSize + dirHashCount * 4 + dirTableLen + fileHashCount * 4 + fileTableLen, 0x10);
        ulong level3Size = romfsHdrSize + dataLen;
        ulong level2Size = AlignUp(level3Size, RomFsBlockSize) / RomFsBlockSize * Sha256Len;
        ulong level1Size = AlignUp(level2Size, RomFsBlockSize) / RomFsBlockSize * Sha256Len;
        ulong masterHashSize = AlignUp(level1Size, RomFsBlockSize) / RomFsBlockSize * Sha256Len;
        ulong level0Size = (ulong)AlignUp(IvfcHeaderSize, IvfcHeaderAlign) + masterHashSize;
        ulong offLevel3 = AlignUp(level0Size, RomFsBlockSize);
        ulong offLevel1Hash = AlignUp(offLevel3 + level3Size, RomFsBlockSize);
        ulong offLevel2Hash = AlignUp(offLevel1Hash + level1Size, RomFsBlockSize);
        ulong totalSize = AlignUp(offLevel2Hash + level2Size, RomFsBlockSize);

        return (totalSize, level0Size, offLevel3, offLevel1Hash, offLevel2Hash);
    }

    public static async Task<Dictionary<string, long>> BuildPatchSizeMapAsync(IReadOnlyList<RomFsFileNode> files, IRomFsFileSource patchSource, Stream? ncchStream = null, long dataBase = 0, CancellationToken ct = default)
    {
        var map = new Dictionary<string, long>();

        bool hasOriginalSource = ncchStream != null && ncchStream != Stream.Null;

        foreach (var file in files)
        {
            Func<CancellationToken, ValueTask<Stream?>>? getOriginal = hasOriginalSource
                ? (ct2 => ReadOriginalSliceAsync(ncchStream!, dataBase, file, ct2))
                : null;

            var stream = await patchSource.OpenFileAsync(file.FullPath, getOriginal, ct);

            if (stream != null)
            {
                await using (stream)
                    map[file.FullPath] = stream.Length;
            }
        }

        return map;
    }

    public static (List<RomFsDirNode> dirs, List<RomFsFileNode> files) ScanFolder(string rootDir)
    {
        var dirs = new List<RomFsDirNode>();
        var files = new List<RomFsFileNode>();

        dirs.Add(new RomFsDirNode
        {
            FullPath = "/",
            Entry = new RomFsDirEntry { Name = string.Empty }
        });

        foreach (var absDir in Directory.EnumerateDirectories(rootDir, "*", SearchOption.AllDirectories).OrderBy(x => x))
        {
            string rel = "/" + Path.GetRelativePath(rootDir, absDir).Replace(Path.DirectorySeparatorChar, '/');
            string name = Path.GetFileName(absDir);

            dirs.Add(new RomFsDirNode
            {
                FullPath = rel,
                Entry = new RomFsDirEntry { Name = name }
            });
        }

        foreach (var absFile in Directory.EnumerateFiles(rootDir, "*", SearchOption.AllDirectories).OrderBy(x => x))
        {
            string rel = "/" + Path.GetRelativePath(rootDir, absFile).Replace(Path.DirectorySeparatorChar, '/');
            string name = Path.GetFileName(absFile);
            long size = new FileInfo(absFile).Length;

            files.Add(new RomFsFileNode
            {
                FullPath = rel,
                DataOffset = 0,
                DataSize = (ulong)size,
                Entry = new RomFsFileEntry { Name = name }
            });
        }

        return (dirs, files);
    }

    public static RomFsUnpackResult ScanFolderAsUnpackResult(string rootDir)
    {
        var (dirs, files) = ScanFolder(rootDir);

        return new RomFsUnpackResult
        {
            IvfcHeader = new IvfcHeader
            {
                Magic = 0x43465649,
                TypeId = 0x10000,
                MasterHashSize = 0,
                Levels = new IvfcLevelEntry[3],
                HeaderSize = IvfcHeader.Size,
            },
            RomFsHeader = new RomFsHeader
            {
                HeaderSize = RomFsHeader.Size,
                DirHashBucketOffset = RomFsHeader.Size,
            },
            DataLevel2Offset = 0,
            Directories = dirs,
            Files = files,
        };
    }

    private static async ValueTask<Stream?> ReadOriginalSliceAsync(Stream ncchStream, long dataBase, RomFsFileNode file, CancellationToken ct)
    {
        if (file.DataSize == 0)
            return new MemoryStream([]);

        ncchStream.Position = dataBase + (long)file.DataOffset;

        byte[] buffer = new byte[(long)file.DataSize];

        await ncchStream.ReadExactlyAsync(buffer, ct);

        return new MemoryStream(buffer);
    }

    private static async Task PackInternalAsync(Stream ncchStream, long dataBase, IReadOnlyList<RomFsDirNode> dirs, IReadOnlyList<RomFsFileNode> files, Stream output, long startPos, long totalBytes, IRomFsFileSource? patchSource = null, Dictionary<string, long>? patchSizeMap = null, Action<long, long>? progress = null, CancellationToken ct = default)
    {
        int dirCount = dirs.Count;
        int fileCount = files.Count;
        uint dirHashCount = GetHashTableCount((uint)dirCount);
        uint fileHashCount = GetHashTableCount((uint)fileCount);
        uint dirTableLen = 0;

        foreach (var d in dirs)
        {
            uint nameBytes = (uint)Encoding.Unicode.GetByteCount(d.Entry.Name);
            dirTableLen += d.Entry.Name.Length == 0 ? DirEntryFixed : (DirEntryFixed + AlignUp(nameBytes, 4));
        }

        uint fileTableLen = 0;

        foreach (var f in files)
        {
            uint nameBytes = (uint)Encoding.Unicode.GetByteCount(f.Entry.Name);
            fileTableLen += FileEntryFixed + AlignUp(nameBytes, 4);
        }

        ulong dataLen = 0;

        foreach (var f in files)
        {
            long size = patchSizeMap?.TryGetValue(f.FullPath, out long ps) == true ? ps : (long)f.DataSize;

            if (size > 0)
                dataLen = AlignUp(dataLen, 0x10) + (ulong)size;
        }

        uint romfsHdrSize = AlignUp(RomFsInfoSize + dirHashCount * 4 + dirTableLen + fileHashCount * 4 + fileTableLen, 0x10);
        ulong level3Size = romfsHdrSize + dataLen;
        ulong level2Size = AlignUp(level3Size, RomFsBlockSize) / RomFsBlockSize * Sha256Len;
        ulong level1Size = AlignUp(level2Size, RomFsBlockSize) / RomFsBlockSize * Sha256Len;
        ulong masterHashSize = AlignUp(level1Size, RomFsBlockSize) / RomFsBlockSize * Sha256Len;
        ulong level0Size = (ulong)AlignUp(IvfcHeaderSize, IvfcHeaderAlign) + masterHashSize;
        ulong off0 = 0;
        ulong offLevel3 = AlignUp(off0 + level0Size, RomFsBlockSize);
        ulong offLevel1Hash = AlignUp(offLevel3 + level3Size, RomFsBlockSize);
        ulong offLevel2Hash = AlignUp(offLevel1Hash + level1Size, RomFsBlockSize);
        ulong totalSize = AlignUp(offLevel2Hash + level2Size, RomFsBlockSize);
        ulong logOff1 = 0;
        ulong logOff2 = AlignUp(logOff1 + level1Size, RomFsBlockSize);
        ulong logOff3 = AlignUp(logOff2 + level2Size, RomFsBlockSize);

        output.Position = startPos + (long)off0;
        byte[] ivfc = new byte[AlignUp(IvfcHeaderSize, IvfcHeaderAlign)];

        ivfc[0] = (byte)'I'; ivfc[1] = (byte)'V';
        ivfc[2] = (byte)'F'; ivfc[3] = (byte)'C';

        BinaryPrimitives.WriteUInt32LittleEndian(ivfc.AsSpan(0x04), 0x10000);
        BinaryPrimitives.WriteUInt32LittleEndian(ivfc.AsSpan(0x08), (uint)masterHashSize);
        WriteIvfcLevel(ivfc, 0x0C, logOff1, level1Size);
        WriteIvfcLevel(ivfc, 0x24, logOff2, level2Size);
        WriteIvfcLevel(ivfc, 0x3C, logOff3, level3Size);
        BinaryPrimitives.WriteUInt32LittleEndian(ivfc.AsSpan(0x54), 0x5C);

        await output.WriteAsync(ivfc, ct);

        uint dirHashOffset = RomFsInfoSize;
        uint dirEntryOffset = dirHashOffset + dirHashCount * 4;
        uint fileHashOffset = dirEntryOffset + dirTableLen;
        uint fileEntryOffset = fileHashOffset + fileHashCount * 4;
        uint dataOffset = AlignUp(fileEntryOffset + fileTableLen, 0x10);

        byte[] meta = new byte[romfsHdrSize];

        BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(0x00), RomFsInfoSize);
        BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(0x04), dirHashOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(0x08), dirHashCount * 4);
        BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(0x0C), dirEntryOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(0x10), dirTableLen);
        BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(0x14), fileHashOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(0x18), fileHashCount * 4);
        BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(0x1C), fileEntryOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(0x20), fileTableLen);
        BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(0x24), dataOffset);

        int dirHashBase = (int)dirHashOffset;
        int fileHashBase = (int)fileHashOffset;
        int dirTableBase = (int)dirEntryOffset;
        int fileTableBase = (int)fileEntryOffset;

        for (uint i = 0; i < dirHashCount; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(dirHashBase + (int)(i * 4)), UnusedEntry);

        for (uint i = 0; i < fileHashCount; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(fileHashBase + (int)(i * 4)), UnusedEntry);


        var dirVaddrMap = new Dictionary<string, uint>();
        uint dirTablePos = 0;

        foreach (var dir in dirs)
        {
            uint nameBytes = dir.Entry.Name.Length > 0 ? (uint)Encoding.Unicode.GetByteCount(dir.Entry.Name) : 0;
            int entryBase = dirTableBase + (int)dirTablePos;
            uint parentVaddr = dir.Entry.Name.Length == 0 ? 0 : dirVaddrMap.TryGetValue(GetParentPath(dir.FullPath), out uint pv) ? pv : 0;

            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(entryBase + 0x00), parentVaddr);
            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(entryBase + 0x04), UnusedEntry);
            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(entryBase + 0x08), UnusedEntry);
            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(entryBase + 0x0C), UnusedEntry);

            uint hashIdx = CalcPathHash(parentVaddr, dir.Entry.Name) % dirHashCount;
            uint prevChain = BinaryPrimitives.ReadUInt32LittleEndian(meta.AsSpan(dirHashBase + (int)(hashIdx * 4)));

            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(entryBase + 0x10), prevChain);
            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(dirHashBase + (int)(hashIdx * 4)), dirTablePos);
            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(entryBase + 0x14), nameBytes);

            if (nameBytes > 0)
            {
                byte[] nameUtf16 = Encoding.Unicode.GetBytes(dir.Entry.Name);

                nameUtf16.CopyTo(meta, entryBase + 0x18);
            }

            dirVaddrMap[dir.FullPath] = dirTablePos;
            dirTablePos += nameBytes == 0 ? DirEntryFixed : (DirEntryFixed + AlignUp(nameBytes, 4));
        }

        foreach (var dir in dirs)
        {
            uint myVaddr = dirVaddrMap[dir.FullPath];
            int myBase = dirTableBase + (int)myVaddr;
            string parentPath = GetParentPath(dir.FullPath);

            if (dir.Entry.Name.Length > 0)
            {
                var nextSibling = dirs
                    .SkipWhile(d => d.FullPath != dir.FullPath)
                    .Skip(1)
                    .FirstOrDefault(d => GetParentPath(d.FullPath) == parentPath);

                uint sibVaddr = nextSibling != null ? dirVaddrMap[nextSibling.FullPath] : UnusedEntry;
                BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(myBase + 0x04), sibVaddr);
            }

            var firstChild = dirs.FirstOrDefault(d => GetParentPath(d.FullPath) == dir.FullPath && d.Entry.Name.Length > 0);

            if (firstChild != null)
                BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(myBase + 0x08), dirVaddrMap[firstChild.FullPath]);
        }

        var dirFirstFile = new Dictionary<string, bool>();
        uint fileTablePos = 0;
        ulong dataAreaPos = 0;

        for (int fi = 0; fi < files.Count; fi++)
        {
            var file = files[fi];
            string parentPath = GetParentPath(file.FullPath);
            uint dirVaddr = dirVaddrMap[parentPath];
            int dirBase = dirTableBase + (int)dirVaddr;
            int entryBase = fileTableBase + (int)fileTablePos;
            uint nameBytes = (uint)Encoding.Unicode.GetByteCount(file.Entry.Name);

            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(entryBase + 0x00), dirVaddr);

            uint nextFileVaddr = fi < files.Count - 1 && GetParentPath(files[fi + 1].FullPath) == parentPath
                ? fileTablePos + FileEntryFixed + AlignUp(nameBytes, 4)
                : UnusedEntry;

            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(entryBase + 0x04), nextFileVaddr);

            if (!dirFirstFile.ContainsKey(parentPath))
            {
                dirFirstFile[parentPath] = true;

                BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(dirBase + 0x0C), fileTablePos);
            }

            if (file.DataSize > 0 || patchSizeMap?.ContainsKey(file.FullPath) == true)
            {
                long actualSize = patchSizeMap?.TryGetValue(file.FullPath, out long ps) == true ? ps : (long)file.DataSize;

                dataAreaPos = AlignUp(dataAreaPos, 0x10);

                BinaryPrimitives.WriteUInt64LittleEndian(meta.AsSpan(entryBase + 0x08), dataAreaPos);
                BinaryPrimitives.WriteUInt64LittleEndian(meta.AsSpan(entryBase + 0x10), (ulong)actualSize);

                dataAreaPos += (ulong)actualSize;
            }
            else
            {
                BinaryPrimitives.WriteUInt64LittleEndian(meta.AsSpan(entryBase + 0x08), 0);
                BinaryPrimitives.WriteUInt64LittleEndian(meta.AsSpan(entryBase + 0x10), 0);
            }

            byte[] nameUtf16 = Encoding.Unicode.GetBytes(file.Entry.Name);
            uint hashIdx = CalcPathHash(dirVaddr, file.Entry.Name) % fileHashCount;
            uint prevChain = BinaryPrimitives.ReadUInt32LittleEndian(meta.AsSpan(fileHashBase + (int)(hashIdx * 4)));

            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(entryBase + 0x18), prevChain);
            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(fileHashBase + (int)(hashIdx * 4)), fileTablePos);
            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(entryBase + 0x1C), nameBytes);
            nameUtf16.CopyTo(meta, entryBase + 0x20);

            fileTablePos += FileEntryFixed + AlignUp(nameBytes, 4);
        }

        long level3AbsStart = startPos + (long)offLevel3;
        var hashingStream = new Level3HashingStream(output);

        output.Position = level3AbsStart;

        await hashingStream.WriteAsync(meta, ct);

        dataAreaPos = 0;

        long cumulativeWritten = 0;

        bool hasOriginalSource = ncchStream != null && ncchStream != Stream.Null;

        foreach (var file in files)
        {
            long actualSize = patchSizeMap?.TryGetValue(file.FullPath, out long ps) == true ? ps : (long)file.DataSize;

            if (actualSize == 0)
                continue;

            dataAreaPos = AlignUp(dataAreaPos, 0x10);

            long fileAbsPos = level3AbsStart + romfsHdrSize + (long)dataAreaPos;

            if (output.Position != fileAbsPos)
            {
                int padSize = (int)(fileAbsPos - output.Position);

                await hashingStream.WriteAsync(new byte[padSize], ct);
            }

            Func<CancellationToken, ValueTask<Stream?>>? getOriginal = hasOriginalSource
                ? (ct2 => ReadOriginalSliceAsync(ncchStream, dataBase, file, ct2))
                : null;

            Stream? patchStream = patchSource != null ? await patchSource.OpenFileAsync(file.FullPath, getOriginal, ct) : null;
            long before = cumulativeWritten;

            if (patchStream != null)
            {
                await using (patchStream)
                    await patchStream.CopyToAsync(hashingStream, patchStream.Length, totalBytes, progress != null ? (w, t) => progress(before + w, t) : null, ct);
            }
            else
            {
                ncchStream.Position = dataBase + (long)file.DataOffset;

                await ncchStream.CopyToAsync(hashingStream, (long)file.DataSize, totalBytes, progress != null ? (w, t) => progress(before + w, t) : null, ct);
            }

            dataAreaPos += (ulong)actualSize;
            cumulativeWritten += actualSize;
        }

        byte[] level3HashResult = hashingStream.GetHashResult();

        output.Position = startPos + (long)offLevel2Hash;
        await output.WriteAsync(level3HashResult, ct);

        await GenHashLevelAsync(output, startPos + (long)offLevel2Hash, startPos + (long)offLevel1Hash, level2Size, ct);
        await GenHashLevelAsync(output, startPos + (long)offLevel1Hash, startPos + (long)(off0 + (ulong)AlignUp(IvfcHeaderSize, IvfcHeaderAlign)), level1Size, ct);
    }

    private static void WriteIvfcLevel(byte[] buf, int offset, ulong logicalOffset, ulong size)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(offset + 0x00), logicalOffset);
        BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(offset + 0x08), size);
        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(offset + 0x10), 12);
    }

    private static async Task GenHashLevelAsync(Stream stream, long dataOffset, long hashOffset, ulong dataSize, CancellationToken ct)
    {
        ulong numBlocks = AlignUp(dataSize, RomFsBlockSize) / RomFsBlockSize;
        byte[] block = new byte[RomFsBlockSize];

        for (ulong i = 0; i < numBlocks; i++)
        {
            stream.Position = dataOffset + (long)(i * RomFsBlockSize);

            ulong remaining = dataSize - i * RomFsBlockSize;
            int copySize = (int)Math.Min(RomFsBlockSize, remaining);

            Array.Clear(block);
            await stream.ReadExactlyAsync(block.AsMemory(0, copySize), ct);

            byte[] hash = SHA256.HashData(block);

            stream.Position = hashOffset + (long)(i * Sha256Len);
            await stream.WriteAsync(hash, ct);
        }
    }

    private static uint CalcPathHash(uint parentVaddr, string name)
    {
        uint hash = parentVaddr ^ 123456789;

        foreach (char c in name)
        {
            hash = (hash >> 5) | (hash << 27);
            hash ^= c;
        }

        return hash;
    }

    private static uint GetHashTableCount(uint num)
    {
        if (num < 3)
            return 3;

        uint count = num;

        if (count < 19)
        {
            if (count % 2 == 0)
                count++;

            return count;
        }

        while (count % 2 == 0 || count % 3 == 0 || count % 5 == 0 || count % 7 == 0 || count % 11 == 0 || count % 13 == 0 || count % 17 == 0)
            count++;

        return count;
    }

    private static string GetParentPath(string fullPath)
    {
        int lastSlash = fullPath.TrimEnd('/').LastIndexOf('/');

        return lastSlash <= 0 ? "/" : fullPath[..lastSlash];
    }

    private static uint AlignUp(uint v, uint a) => (v + a - 1) & ~(a - 1);

    private static ulong AlignUp(ulong v, ulong a) => (v + a - 1) & ~(a - 1);

    private static int AlignUp(int v, int a) => (v + a - 1) & ~(a - 1);

    private static uint AlignUp(uint v, int a) => AlignUp(v, (uint)a);

    private class Level3HashingStream(Stream inner) : Stream
    {
        private readonly byte[] _block = new byte[RomFsBlockSize];
        private readonly List<byte[]> _hashes = [];
        private int _blockPos = 0;

        public byte[] GetHashResult()
        {
            if (_blockPos > 0)
            {
                Array.Clear(_block, _blockPos, RomFsBlockSize - _blockPos);
                _hashes.Add(SHA256.HashData(_block));
            }

            byte[] result = new byte[_hashes.Count * Sha256Len];

            for (int i = 0; i < _hashes.Count; i++)
                _hashes[i].CopyTo(result, i * Sha256Len);

            return result;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
        {
            await inner.WriteAsync(buffer, ct);

            int offset = 0;

            while (offset < buffer.Length)
            {
                int toCopy = Math.Min(RomFsBlockSize - _blockPos, buffer.Length - offset);

                buffer.Slice(offset, toCopy).CopyTo(_block.AsMemory(_blockPos));
                _blockPos += toCopy;
                offset += toCopy;

                if (_blockPos == RomFsBlockSize)
                {
                    _hashes.Add(SHA256.HashData(_block));
                    _blockPos = 0;
                }
            }
        }

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
    }
}