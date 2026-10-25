using Common;
using NSW.HacPack.Models;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NSW.HacPack.Services;

public static class RomfsBuilder
{
    private const uint EntryEmpty = 0xFFFFFFFF;
    private const ulong FilePartitionOfs = 0x200;
    private const int HashBlockSize = IvfcConstants.HashBlockSize;

    public static void BuildRomfsImage(string inDirPath, Stream outStream, out ulong outSize, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        ConstructRomfsStructure(inDirPath, outStream, 0, progress, ct);

        outSize = (ulong)outStream.Position;

        ulong paddingSize = HashBlockSize - outSize % HashBlockSize;

        if (paddingSize != HashBlockSize)
            outStream.Write(new byte[paddingSize]);
    }

    private static void ConstructRomfsStructure(string inDirPath, Stream fout, long baseOffset, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        var rootCtx = new RomfsDirentCtx { SumPath = inDirPath, CurPath = string.Empty };

        rootCtx.Parent = rootCtx;

        var romfsCtx = new RomfsCtx { DirTableSize = 0x18, NumDirs = 1 };

        TraverseDirectoryTree(rootCtx, romfsCtx);

        uint dirHashCount = GetHashTableCount((uint)romfsCtx.NumDirs);
        uint fileHashCount = GetHashTableCount((uint)romfsCtx.NumFiles);

        romfsCtx.DirHashTableSize = 4 * dirHashCount;
        romfsCtx.FileHashTableSize = 4 * fileHashCount;

        uint[] dirHashTable = new uint[dirHashCount];
        uint[] fileHashTable = new uint[fileHashCount];

        for (int i = 0; i < dirHashCount; i++) 
            dirHashTable[i] = EntryEmpty;

        for (int i = 0; i < fileHashCount; i++)
            fileHashTable[i] = EntryEmpty;

        byte[] dirTableBuf = new byte[romfsCtx.DirTableSize];
        byte[] fileTableBuf = new byte[romfsCtx.FileTableSize];
        var curFile = romfsCtx.Files;
        uint fileEntryOffset = 0;

        while (curFile != null)
        {
            romfsCtx.FilePartitionSize = Align64(romfsCtx.FilePartitionSize, 0x10);
            curFile.Offset = romfsCtx.FilePartitionSize;
            romfsCtx.FilePartitionSize += curFile.Size;
            curFile.EntryOffset = fileEntryOffset;
            fileEntryOffset += 0x20 + Align((uint)System.Text.Encoding.UTF8.GetByteCount(curFile.CurPath) - 1, 4);
            curFile = curFile.Next;
        }

        var curDir = rootCtx;
        uint dirEntryOffset = 0;

        while (curDir != null)
        {
            curDir.EntryOffset = dirEntryOffset;
            dirEntryOffset += curDir == rootCtx ? 0x18u : 0x18u + Align((uint)System.Text.Encoding.UTF8.GetByteCount(curDir.CurPath) - 1, 4);
            curDir = curDir.Next;
        }

        curFile = romfsCtx.Files;

        while (curFile != null)
        {
            WriteFileEntry(fileTableBuf, curFile, fileHashTable, fileHashCount);

            curFile = curFile.Next;
        }

        curDir = rootCtx;

        while (curDir != null)
        {
            WriteDirEntry(dirTableBuf, curDir, rootCtx, dirHashTable, dirHashCount);

            curDir = curDir.Next;
        }

        ulong dirHashTableOfs = Align64(romfsCtx.FilePartitionSize + FilePartitionOfs, 4);
        ulong dirTableOfs = dirHashTableOfs + romfsCtx.DirHashTableSize;
        ulong fileHashTableOfs = dirTableOfs + romfsCtx.DirTableSize;
        ulong fileTableOfs = fileHashTableOfs + romfsCtx.FileHashTableSize;

        var header = new RomfsHeader
        {
            HeaderSize = (ulong)Marshal.SizeOf<RomfsHeader>(),
            FilePartitionOfs = FilePartitionOfs,
            DirHashTableOfs = dirHashTableOfs,
            DirHashTableSize = romfsCtx.DirHashTableSize,
            DirTableOfs = dirTableOfs,
            DirTableSize = romfsCtx.DirTableSize,
            FileHashTableOfs = fileHashTableOfs,
            FileHashTableSize = romfsCtx.FileHashTableSize,
            FileTableOfs = fileTableOfs,
            FileTableSize = romfsCtx.FileTableSize,
        };

        fout.Seek(baseOffset, SeekOrigin.Begin);
        NcaStructHelper.WriteStruct(fout, header);

        ulong totalSize = 0;
        var tmp = romfsCtx.Files;

        while (tmp != null) 
        {
            totalSize += tmp.Size; 
            tmp = tmp.Next;
        }

        var reporter = new ProgressReporter("RomFS 빌드 중", string.Empty, (long)totalSize, progress);
        const int bufSize = 0x800000;
        var buf = System.Buffers.ArrayPool<byte>.Shared.Rent(bufSize);

        try
        {
            curFile = romfsCtx.Files;

            while (curFile != null)
            {
                ct.ThrowIfCancellationRequested();

                using var fin = new FileStream(curFile.SumPath, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan); 

                fout.Seek(baseOffset + (long)(curFile.Offset + FilePartitionOfs), SeekOrigin.Begin);

                ulong remaining = curFile.Size;                

                while (remaining > 0)
                {
                    ct.ThrowIfCancellationRequested();

                    int toRead = (int)Math.Min(bufSize, remaining);
                    int read = fin.Read(buf.AsSpan(0, toRead));

                    if (read <= 0) 
                        break;

                    fout.Write(buf.AsSpan(0, read));

                    remaining -= (ulong)read;

                    reporter.AddProgress(read);
                }

                curFile = curFile.Next;
            }

            reporter.ForceReport();
        }
        finally
        {
            System.Buffers.ArrayPool<byte>.Shared.Return(buf);
        }

        fout.Seek(baseOffset + (long)dirHashTableOfs, SeekOrigin.Begin);

        byte[] dirHashBytes = new byte[romfsCtx.DirHashTableSize];

        Buffer.BlockCopy(dirHashTable, 0, dirHashBytes, 0, (int)romfsCtx.DirHashTableSize);
        fout.Write(dirHashBytes);
        fout.Write(dirTableBuf);

        byte[] fileHashBytes = new byte[romfsCtx.FileHashTableSize];

        Buffer.BlockCopy(fileHashTable, 0, fileHashBytes, 0, (int)romfsCtx.FileHashTableSize);
        fout.Write(fileHashBytes);
        fout.Write(fileTableBuf);
    }

    private static void TraverseDirectoryTree(RomfsDirentCtx parent, RomfsCtx romfsCtx)
    {
        RomfsDirentCtx? childDirTree = null;
        RomfsFentCtx? childFileTree = null;

        foreach (var entry in Directory.EnumerateFileSystemEntries(parent.SumPath))
        {
            string name = Path.GetFileName(entry);
            string curPath = "/" + name;
            string sumPath = entry;
            var attr = File.GetAttributes(entry);

            if ((attr & FileAttributes.Directory) != 0)
            {
                var curDir = new RomfsDirentCtx
                {
                    Parent = parent,
                    SumPath = sumPath,
                    CurPath = curPath
                };

                romfsCtx.NumDirs++;
                romfsCtx.DirTableSize += 0x18 + Align((uint)System.Text.Encoding.UTF8.GetByteCount(curDir.CurPath) - 1, 4);

                InsertDirSibling(ref childDirTree, curDir);
                InsertDirNext(parent, curDir);
            }
            else
            {
                var curFile = new RomfsFentCtx
                {
                    Parent = parent,
                    SumPath = sumPath,
                    CurPath = curPath,
                    Size = (ulong)new FileInfo(entry).Length
                };

                romfsCtx.NumFiles++;
                romfsCtx.FileTableSize += 0x20 + Align((uint)System.Text.Encoding.UTF8.GetByteCount(curFile.CurPath) - 1, 4);

                InsertFileSibling(ref childFileTree, curFile);
                InsertFileNext(romfsCtx, curFile);
            }
        }

        parent.Child = childDirTree;
        parent.File = childFileTree;

        var cur = childDirTree;

        while (cur != null)
        {
            TraverseDirectoryTree(cur, romfsCtx);

            cur = cur.Sibling;
        }
    }

    private static void InsertDirSibling(ref RomfsDirentCtx? tree, RomfsDirentCtx node)
    {
        if (tree == null || string.Compare(node.SumPath, tree.SumPath, StringComparison.Ordinal) < 0)
        {
            node.Sibling = tree; tree = node;
            return; 
        }

        var prev = tree;
        var cur = tree.Sibling;

        while (cur != null && string.Compare(node.SumPath, cur.SumPath, StringComparison.Ordinal) >= 0)
        { 
            prev = cur;
            cur = cur.Sibling;
        }

        prev.Sibling = node;
        node.Sibling = cur;
    }

    private static void InsertDirNext(RomfsDirentCtx parent, RomfsDirentCtx node)
    {
        var tmp = parent.Next; 
        var tmpPrev = parent;

        while (tmp != null && string.Compare(node.SumPath, tmp.SumPath, StringComparison.Ordinal) >= 0)
        { 
            tmpPrev = tmp; 
            tmp = tmp.Next;
        }

        tmpPrev.Next = node;
        node.Next = tmp;
    }

    private static void InsertFileSibling(ref RomfsFentCtx? tree, RomfsFentCtx node)
    {
        if (tree == null || string.Compare(node.SumPath, tree.SumPath, StringComparison.Ordinal) < 0)
        { 
            node.Sibling = tree; 
            tree = node;
            return; 
        }

        var prev = tree; 
        var cur = tree.Sibling;

        while (cur != null && string.Compare(node.SumPath, cur.SumPath, StringComparison.Ordinal) >= 0)
        { 
            prev = cur;
            cur = cur.Sibling;
        }

        prev.Sibling = node;
        node.Sibling = cur;
    }

    private static void InsertFileNext(RomfsCtx ctx, RomfsFentCtx node)
    {
        if (ctx.Files == null || string.Compare(node.SumPath, ctx.Files.SumPath, StringComparison.Ordinal) < 0)
        { 
            node.Next = ctx.Files;
            ctx.Files = node; 
            return; 
        }

        var prev = ctx.Files; 
        var cur = ctx.Files.Next;

        while (cur != null && string.Compare(node.SumPath, cur.SumPath, StringComparison.Ordinal) >= 0)
        { 
            prev = cur; 
            cur = cur.Next;
        }

        prev.Next = node; 
        node.Next = cur;
    }

    private static void WriteFileEntry(byte[] table, RomfsFentCtx file, uint[] hashTable, uint hashCount)
    {
        int ofs = (int)file.EntryOffset;
        Span<byte> tableSpan = table.AsSpan();
        Span<byte> nameBuf = stackalloc byte[512];
        int nameByteCount = System.Text.Encoding.UTF8.GetBytes(file.CurPath.AsSpan(1), nameBuf);
        uint nameSize = (uint)nameByteCount;

        WriteU32(tableSpan, ofs + 0x00, file.Parent!.EntryOffset);
        WriteU32(tableSpan, ofs + 0x04, file.Sibling == null ? EntryEmpty : file.Sibling.EntryOffset);
        WriteU64(tableSpan, ofs + 0x08, file.Offset);
        WriteU64(tableSpan, ofs + 0x10, file.Size);

        uint hash = CalcPathHash(file.Parent.EntryOffset, nameBuf[..(int)nameSize]);

        WriteU32(tableSpan, ofs + 0x18, hashTable[hash % hashCount]);

        hashTable[hash % hashCount] = file.EntryOffset;

        WriteU32(tableSpan, ofs + 0x1C, nameSize);

        nameBuf[..(int)nameSize].CopyTo(tableSpan[(ofs + 0x20)..]);
    }

    private static unsafe uint CalcPathHash(uint parent, ReadOnlySpan<byte> name)
    {
        uint hash = parent ^ 123456789;

        fixed (byte* pName = name)
        {
            for (int i = 0; i < name.Length; i++)
            {
                hash = (hash >> 5) | (hash << 27);
                hash ^= pName[i];
            }
        }

        return hash;
    }

    private static void WriteDirEntry(byte[] table, RomfsDirentCtx dir, RomfsDirentCtx root, uint[] hashTable, uint hashCount)
    {
        int ofs = (int)dir.EntryOffset;
        Span<byte> tableSpan = table.AsSpan();

        WriteU32(tableSpan, ofs + 0x00, dir.Parent!.EntryOffset);
        WriteU32(tableSpan, ofs + 0x04, dir.Sibling == null ? EntryEmpty : dir.Sibling.EntryOffset);
        WriteU32(tableSpan, ofs + 0x08, dir.Child == null ? EntryEmpty : dir.Child.EntryOffset);
        WriteU32(tableSpan, ofs + 0x0C, dir.File == null ? EntryEmpty : dir.File.EntryOffset);

        bool isRoot = dir == root;
        uint parentOfs = isRoot ? 0 : dir.Parent.EntryOffset;
        Span<byte> nameBuf = stackalloc byte[512];
        int nameByteCount = isRoot ? 0 : System.Text.Encoding.UTF8.GetBytes(dir.CurPath.AsSpan(1), nameBuf);
        uint nameSize = (uint)nameByteCount;
        uint hash = CalcPathHash(parentOfs, nameBuf[..(int)nameSize]);

        WriteU32(tableSpan, ofs + 0x10, hashTable[hash % hashCount]);

        hashTable[hash % hashCount] = dir.EntryOffset;

        WriteU32(tableSpan, ofs + 0x14, nameSize);

        if (!isRoot)
            nameBuf[..(int)nameSize].CopyTo(tableSpan[(ofs + 0x18)..]);
    }

    private static uint GetHashTableCount(uint numEntries)
    {
        if (numEntries < 3) 
            return 3;

        if (numEntries < 19) 
            return numEntries | 1;

        uint count = numEntries;

        while (count % 2 == 0 || count % 3 == 0 || count % 5 == 0 || count % 7 == 0 || count % 11 == 0 || count % 13 == 0 || count % 17 == 0)
            count++;

        return count;
    }

    private static uint Align(uint v, uint a) => v + a - 1 & ~(a - 1);

    private static ulong Align64(ulong v, ulong a) => v + a - 1 & ~(a - 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteU32(Span<byte> buf, int ofs, uint v) => System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(buf[ofs..], v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteU64(Span<byte> buf, int ofs, ulong v) => System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(buf[ofs..], v);
}