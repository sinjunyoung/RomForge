using N3DS.Core.Models;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace N3DS.Core.Services;

public static class RomFsUnpacker
{
    private const int MediaUnit = 0x200;

    public static async Task<RomFsUnpackResult> UnpackAsync(Stream ncchStream, NcchHeader ncchHeader, CancellationToken ct = default)
    {
        if (ncchHeader.RomfsOffset == 0 || ncchHeader.RomfsSize == 0)
            throw new InvalidOperationException("이 NCCH에는 RomFS가 없습니다.");

        long ivfcAbsOffset = (long)ncchHeader.RomfsOffset * MediaUnit;
        byte[] ivfcBuf = new byte[IvfcHeader.Size];

        ncchStream.Position = ivfcAbsOffset;

        await ncchStream.ReadExactlyAsync(ivfcBuf, ct);

        var ivfc = IvfcHeader.Parse(ivfcBuf);

        if (ivfc.Magic != 0x43465649)
            throw new InvalidDataException("IVFC 매직 불일치");

        if (ivfc.TypeId != 0x10000)
            throw new InvalidDataException("IVFC TypeId가 CTR RomFS(0x10000)가 아닙니다.");

        long dataLevel2AbsOffset = ivfcAbsOffset + ivfc.GetDataLevel2Offset();
        byte[] romfsBuf = new byte[RomFsHeader.Size];

        ncchStream.Position = dataLevel2AbsOffset;

        await ncchStream.ReadExactlyAsync(romfsBuf, ct);

        var romfs = RomFsHeader.Parse(romfsBuf);

        if (romfs.HeaderSize != RomFsHeader.Size)
            throw new InvalidDataException($"RomFS 헤더 크기 이상: 0x{romfs.HeaderSize:X}");

        if (romfs.DirHashBucketOffset != RomFsHeader.Size)
            throw new InvalidDataException("RomFS DirHashBucket 오프셋 이상");

        byte[] dirTable = new byte[romfs.DirEntrySize];
        byte[] fileTable = new byte[romfs.FileEntrySize];

        ncchStream.Position = dataLevel2AbsOffset + romfs.DirEntryOffset;

        await ncchStream.ReadExactlyAsync(dirTable, ct);

        ncchStream.Position = dataLevel2AbsOffset + romfs.FileEntryOffset;

        await ncchStream.ReadExactlyAsync(fileTable, ct);

        var (dirs, files) = ParseEntryTables(dirTable, fileTable);

        return new RomFsUnpackResult
        {
            IvfcHeader = ivfc,
            RomFsHeader = romfs,
            DataLevel2Offset = dataLevel2AbsOffset,
            Directories = dirs,
            Files = files,
        };
    }

    public static async Task SaveToDirectoryAsync(Stream ncchStream, RomFsUnpackResult result, string outputDir, Action<long, long>? reporter = null, CancellationToken ct = default)
    {
        long dataBase = result.DataLevel2Offset + result.RomFsHeader.DataOffset;
        long totalBytes = result.Files.Sum(f => (long)f.DataSize);
        long currentBytes = 0;

        if (totalBytes > 0) reporter?.Invoke(0, totalBytes);

        foreach (var file in result.Files)
        {
            string fullPath = Path.Combine(outputDir, file.FullPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            if (file.DataSize == 0)
            {
                await File.WriteAllBytesAsync(fullPath, [], ct);
                continue;
            }

            await using var fs = File.Open(fullPath, FileMode.Create, FileAccess.Write);

            ncchStream.Position = dataBase + (long)file.DataOffset;

            var pool = ArrayPool<byte>.Shared;
            byte[] buf = pool.Rent(128 * 1024);

            try
            {
                long remaining = (long)file.DataSize;

                while (remaining > 0)
                {
                    ct.ThrowIfCancellationRequested();

                    int toRead = (int)Math.Min(buf.Length, remaining);
                    int read = await ncchStream.ReadAsync(buf.AsMemory(0, toRead), ct);

                    if (read == 0)
                        break;

                    await fs.WriteAsync(buf.AsMemory(0, read), ct);

                    remaining -= read;
                    currentBytes += read;

                    reporter?.Invoke(currentBytes, totalBytes);
                }
            }
            finally 
            { 
                pool.Return(buf);
            }
        }
    }

    private static (List<RomFsDirNode> dirs, List<RomFsFileNode> files) ParseEntryTables(byte[] dirTable, byte[] fileTable)
    {
        var dirVaddrToIndex = new Dictionary<uint, int>();
        var dirs = new List<RomFsDirNode>();
        var files = new List<RomFsFileNode>();

        for (uint vaddr = 0; vaddr < dirTable.Length;)
        {
            var entry = ParseDirEntry(dirTable, (int)vaddr);
            string fullPath;

            if (vaddr == 0)
                fullPath = "/";
            else
            {
                string parentPath = dirs[dirVaddrToIndex[entry.ParentOffset]].FullPath;

                fullPath = parentPath.TrimEnd('/') + "/" + entry.Name;
            }

            int idx = dirs.Count;

            dirs.Add(new RomFsDirNode { FullPath = fullPath, Entry = entry });

            dirVaddrToIndex[vaddr] = idx;

            vaddr += (uint)entry.TotalSize;
        }

        for (uint vaddr = 0; vaddr < fileTable.Length;)
        {
            var entry = ParseFileEntry(fileTable, (int)vaddr);
            string parentPath = dirs[dirVaddrToIndex[entry.ParentOffset]].FullPath;
            string fullPath = parentPath.TrimEnd('/') + "/" + entry.Name;

            files.Add(new RomFsFileNode
            {
                FullPath = fullPath,
                DataOffset = entry.DataOffset,
                DataSize = entry.DataSize,
                Entry = entry,
            });

            vaddr += (uint)entry.TotalSize;
        }

        return (dirs, files);
    }

    private static RomFsDirEntry ParseDirEntry(byte[] table, int offset)
    {
        uint nameSize = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(offset + 0x14));
        string name = nameSize > 0 ? Encoding.Unicode.GetString(table, offset + 0x18, (int)nameSize) : string.Empty;

        return new RomFsDirEntry
        {
            ParentOffset = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(offset + 0x00)),
            SiblingOffset = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(offset + 0x04)),
            ChildDirOffset = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(offset + 0x08)),
            ChildFileOffset = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(offset + 0x0C)),
            HashSiblingOffset = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(offset + 0x10)),
            NameSize = nameSize,
            Name = name,
        };
    }

    private static RomFsFileEntry ParseFileEntry(byte[] table, int offset)
    {
        uint nameSize = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(offset + 0x1C));
        string name = nameSize > 0 ? Encoding.Unicode.GetString(table, offset + 0x20, (int)nameSize) : string.Empty;

        return new RomFsFileEntry
        {
            ParentOffset = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(offset + 0x00)),
            SiblingOffset = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(offset + 0x04)),
            DataOffset = BinaryPrimitives.ReadUInt64LittleEndian(table.AsSpan(offset + 0x08)),
            DataSize = BinaryPrimitives.ReadUInt64LittleEndian(table.AsSpan(offset + 0x10)),
            HashSiblingOffset = BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(offset + 0x18)),
            NameSize = nameSize,
            Name = name,
        };
    }
}