using N3DS.Core.IO;
using N3DS.Core.Models;
using System.Buffers.Binary;
using System.Text;

namespace N3DS.Core.Services;

public static class RomFsMetadataBuilder
{
    public readonly record struct Result(byte[] Meta, uint RomFsHdrSize, ulong Off0, ulong OffLevel3, ulong OffLevel1Hash, ulong OffLevel2Hash, ulong Level1Size, ulong Level2Size);

    public static async Task<Result> BuildAsync(Stream output, long startPos, IReadOnlyList<RomFsDirNode> dirs, IReadOnlyList<RomFsFileNode> files, Dictionary<string, long>? patchSizeMap, CancellationToken ct)
    {
        uint dirHashCount = RomFsFormat.GetHashTableCount((uint)dirs.Count);
        uint fileHashCount = RomFsFormat.GetHashTableCount((uint)files.Count);
        uint dirTableLen = 0;

        foreach (var d in dirs)
        {
            uint nameBytes = (uint)Encoding.Unicode.GetByteCount(d.Entry.Name);

            dirTableLen += d.Entry.Name.Length == 0 ? RomFsFormat.DirEntryFixed : (RomFsFormat.DirEntryFixed + RomFsFormat.AlignUp(nameBytes, 4));
        }

        uint fileTableLen = 0;

        foreach (var f in files)
        {
            uint nameBytes = (uint)Encoding.Unicode.GetByteCount(f.Entry.Name);

            fileTableLen += RomFsFormat.FileEntryFixed + RomFsFormat.AlignUp(nameBytes, 4);
        }

        ulong dataLen = 0;

        foreach (var f in files)
        {
            long size = patchSizeMap?.TryGetValue(f.FullPath, out long ps) == true ? ps : (long)f.DataSize;

            if (size > 0)
                dataLen = RomFsFormat.AlignUp(dataLen, 0x10) + (ulong)size;
        }

        uint romfsHdrSize = RomFsFormat.AlignUp(RomFsFormat.RomFsInfoSize + dirHashCount * 4 + dirTableLen + fileHashCount * 4 + fileTableLen, 0x10);
        ulong level3Size = romfsHdrSize + dataLen;
        ulong level2Size = RomFsFormat.AlignUp(level3Size, RomFsFormat.BlockSize) / RomFsFormat.BlockSize * RomFsFormat.Sha256Len;
        ulong level1Size = RomFsFormat.AlignUp(level2Size, RomFsFormat.BlockSize) / RomFsFormat.BlockSize * RomFsFormat.Sha256Len;
        ulong masterHashSize = RomFsFormat.AlignUp(level1Size, RomFsFormat.BlockSize) / RomFsFormat.BlockSize * RomFsFormat.Sha256Len;
        ulong level0Size = (ulong)RomFsFormat.AlignUp(RomFsFormat.IvfcHeaderSize, RomFsFormat.IvfcHeaderAlign) + masterHashSize;
        ulong off0 = 0;
        ulong offLevel3 = RomFsFormat.AlignUp(off0 + level0Size, RomFsFormat.BlockSize);
        ulong offLevel1Hash = RomFsFormat.AlignUp(offLevel3 + level3Size, RomFsFormat.BlockSize);
        ulong offLevel2Hash = RomFsFormat.AlignUp(offLevel1Hash + level1Size, RomFsFormat.BlockSize);
        ulong logOff1 = 0;
        ulong logOff2 = RomFsFormat.AlignUp(logOff1 + level1Size, RomFsFormat.BlockSize);
        ulong logOff3 = RomFsFormat.AlignUp(logOff2 + level2Size, RomFsFormat.BlockSize);

        output.Position = startPos + (long)off0;

        byte[] ivfc = new byte[RomFsFormat.AlignUp(RomFsFormat.IvfcHeaderSize, RomFsFormat.IvfcHeaderAlign)];

        ivfc[0] = (byte)'I'; ivfc[1] = (byte)'V';
        ivfc[2] = (byte)'F'; ivfc[3] = (byte)'C';

        BinaryPrimitives.WriteUInt32LittleEndian(ivfc.AsSpan(0x04), 0x10000);
        BinaryPrimitives.WriteUInt32LittleEndian(ivfc.AsSpan(0x08), (uint)masterHashSize);
        IvfcHashTreeWriter.WriteIvfcLevel(ivfc, 0x0C, logOff1, level1Size);
        IvfcHashTreeWriter.WriteIvfcLevel(ivfc, 0x24, logOff2, level2Size);
        IvfcHashTreeWriter.WriteIvfcLevel(ivfc, 0x3C, logOff3, level3Size);
        BinaryPrimitives.WriteUInt32LittleEndian(ivfc.AsSpan(0x54), 0x5C);
        await output.WriteAsync(ivfc, ct);

        uint dirHashOffset = RomFsFormat.RomFsInfoSize;
        uint dirEntryOffset = dirHashOffset + dirHashCount * 4;
        uint fileHashOffset = dirEntryOffset + dirTableLen;
        uint fileEntryOffset = fileHashOffset + fileHashCount * 4;
        uint dataOffset = RomFsFormat.AlignUp(fileEntryOffset + fileTableLen, 0x10);
        byte[] meta = new byte[romfsHdrSize];

        BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(0x00), RomFsFormat.RomFsInfoSize);
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
            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(dirHashBase + (int)(i * 4)), RomFsFormat.UnusedEntry);

        for (uint i = 0; i < fileHashCount; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(fileHashBase + (int)(i * 4)), RomFsFormat.UnusedEntry);

        var dirVaddrMap = new Dictionary<string, uint>();
        uint dirTablePos = 0;

        foreach (var dir in dirs)
        {
            uint nameBytes = dir.Entry.Name.Length > 0 ? (uint)Encoding.Unicode.GetByteCount(dir.Entry.Name) : 0;
            int entryBase = dirTableBase + (int)dirTablePos;
            uint parentVaddr = dir.Entry.Name.Length == 0 ? 0 : dirVaddrMap.TryGetValue(RomFsFormat.GetParentPath(dir.FullPath), out uint pv) ? pv : 0;

            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(entryBase + 0x00), parentVaddr);
            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(entryBase + 0x04), RomFsFormat.UnusedEntry);
            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(entryBase + 0x08), RomFsFormat.UnusedEntry);
            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(entryBase + 0x0C), RomFsFormat.UnusedEntry);

            uint hashIdx = RomFsFormat.CalcPathHash(parentVaddr, dir.Entry.Name) % dirHashCount;
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
            dirTablePos += nameBytes == 0 ? RomFsFormat.DirEntryFixed : (RomFsFormat.DirEntryFixed + RomFsFormat.AlignUp(nameBytes, 4));
        }

        foreach (var dir in dirs)
        {
            uint myVaddr = dirVaddrMap[dir.FullPath];
            int myBase = dirTableBase + (int)myVaddr;
            string parentPath = RomFsFormat.GetParentPath(dir.FullPath);

            if (dir.Entry.Name.Length > 0)
            {
                var nextSibling = dirs
                    .SkipWhile(d => d.FullPath != dir.FullPath)
                    .Skip(1)
                    .FirstOrDefault(d => RomFsFormat.GetParentPath(d.FullPath) == parentPath);

                uint sibVaddr = nextSibling != null ? dirVaddrMap[nextSibling.FullPath] : RomFsFormat.UnusedEntry;

                BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(myBase + 0x04), sibVaddr);
            }

            var firstChild = dirs.FirstOrDefault(d => RomFsFormat.GetParentPath(d.FullPath) == dir.FullPath && d.Entry.Name.Length > 0);

            if (firstChild != null)
                BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(myBase + 0x08), dirVaddrMap[firstChild.FullPath]);
        }

        var dirFirstFile = new Dictionary<string, bool>();
        uint fileTablePos = 0;
        ulong dataAreaPos = 0;

        for (int fi = 0; fi < files.Count; fi++)
        {
            var file = files[fi];
            string parentPath = RomFsFormat.GetParentPath(file.FullPath);
            uint dirVaddr = dirVaddrMap[parentPath];
            int dirBase = dirTableBase + (int)dirVaddr;
            int entryBase = fileTableBase + (int)fileTablePos;
            uint nameBytes = (uint)Encoding.Unicode.GetByteCount(file.Entry.Name);

            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(entryBase + 0x00), dirVaddr);

            uint nextFileVaddr = fi < files.Count - 1 && RomFsFormat.GetParentPath(files[fi + 1].FullPath) == parentPath ? fileTablePos + RomFsFormat.FileEntryFixed + RomFsFormat.AlignUp(nameBytes, 4) : RomFsFormat.UnusedEntry;

            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(entryBase + 0x04), nextFileVaddr);

            if (!dirFirstFile.ContainsKey(parentPath))
            {
                dirFirstFile[parentPath] = true;

                BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(dirBase + 0x0C), fileTablePos);
            }

            if (file.DataSize > 0 || patchSizeMap?.ContainsKey(file.FullPath) == true)
            {
                long actualSize = patchSizeMap?.TryGetValue(file.FullPath, out long ps) == true ? ps : (long)file.DataSize;

                dataAreaPos = RomFsFormat.AlignUp(dataAreaPos, 0x10);

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
            uint hashIdx = RomFsFormat.CalcPathHash(dirVaddr, file.Entry.Name) % fileHashCount;
            uint prevChain = BinaryPrimitives.ReadUInt32LittleEndian(meta.AsSpan(fileHashBase + (int)(hashIdx * 4)));

            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(entryBase + 0x18), prevChain);
            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(fileHashBase + (int)(hashIdx * 4)), fileTablePos);
            BinaryPrimitives.WriteUInt32LittleEndian(meta.AsSpan(entryBase + 0x1C), nameBytes);
            nameUtf16.CopyTo(meta, entryBase + 0x20);

            fileTablePos += RomFsFormat.FileEntryFixed + RomFsFormat.AlignUp(nameBytes, 4);
        }

        return new Result(meta, romfsHdrSize, off0, offLevel3, offLevel1Hash, offLevel2Hash, level1Size, level2Size);
    }
}