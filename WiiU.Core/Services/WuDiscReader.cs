using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using WiiU.Core.Models;

namespace WiiU.Core.Services;

public sealed class WuDiscReader
{
    private const long DiscSectorSize = 0x8000;
    private const uint PartitionTableMagic = 0xCCA6E67B;
    private const uint PartitionHeaderMagic = 0xCC93A4F5;
    private const uint FstMagic = 0x46535400;

    private readonly WudReader _wud;

    public byte[] DiscKey { get; }

    public WuFstVolume SiVolume { get; private set; } = null!;

    public WuFstVolume GmVolume { get; private set; } = null!;

    public int GmPartitionIndex { get; private set; }

    public string GmPartitionName { get; private set; } = "";

    private WuDiscReader(WudReader wud, byte[] discKey)
    {
        _wud = wud;
        DiscKey = discKey;
    }

    public static byte[]? FindDiscKey(WudReader wud, string wudPath, IReadOnlyList<byte[]> candidates)
    {
        var header = new byte[16 * 3];
        int got = wud.ReadData(header, DiscSectorSize * 3 + 0x100);

        if (got == header.Length)
        {
            var iv = header.AsSpan(0, 16).ToArray();
            var ciphertext = header.AsSpan(16, 32).ToArray();
            var scratch = new byte[32];

            foreach (var key in candidates)
            {
                Array.Copy(ciphertext, scratch, 32);

                AesCbcDecryptInPlace(scratch, 32, key, iv);

                if (Array.TrueForAll(scratch, b => b == 0))
                    return key;
            }
        }

        string keyPath = Path.ChangeExtension(wudPath, ".key");

        if (File.Exists(keyPath))
        {
            var bytes = File.ReadAllBytes(keyPath);

            if (bytes.Length == 16)
                return bytes;
        }

        return null;
    }

    public static WuDiscReader Open(WudReader wud, byte[] discKey)
    {
        var reader = new WuDiscReader(wud, discKey);

        reader.Initialize();

        return reader;
    }

    private void Initialize()
    {
        var table = new byte[DiscSectorSize];

        if (_wud.ReadData(table, DiscSectorSize * 3) != table.Length)
            throw new InvalidDataException("Could not read partition table sector.");

        AesCbcDecryptInPlace(table, table.Length, DiscKey, new byte[16]);

        uint magic = BE32(table, 0);

        if (magic != PartitionTableMagic)
            throw new InvalidDataException("Partition table signature mismatch — wrong disc key?");

        uint blockSize = BE32(table, 4);

        if (blockSize != DiscSectorSize)
            throw new InvalidDataException($"Unexpected partition table block size 0x{blockSize:X}.");

        uint numPartitions = BE32(table, 0x1C);

        if (numPartitions > 30)
            throw new InvalidDataException($"Disc image exceeds the supported partition count ({numPartitions}).");

        int siIndex = -1, gmIndex = -1;
        var addresses = new uint[numPartitions];
        var names = new string[numPartitions];

        for (int i = 0; i < numPartitions; i++)
        {
            int entryOffset = 0x800 + i * 0x80;
            string name = ReadNullTerminatedAscii(table, entryOffset, 31);
            uint address = BE32(table, entryOffset + 0x20);

            addresses[i] = address;
            names[i] = name;

            if (name.Length >= 2 && name[0] == 'S' && name[1] == 'I')
            {
                if (siIndex != -1)
                    throw new InvalidDataException("Disc image has multiple SI partitions — not supported.");

                siIndex = i;
            }

            if (name.Length >= 2 && name[0] == 'G' && name[1] == 'M' && gmIndex == -1)
                gmIndex = i;
        }

        if (siIndex == -1 || gmIndex == -1)
            throw new InvalidDataException("Disc image has no SI or GM partition.");

        long siBase = addresses[siIndex] * DiscSectorSize;
        long gmBase = addresses[gmIndex] * DiscSectorSize;
        var siHeader = ReadPartitionHeader(siBase);
        var gmHeader = ReadPartitionHeader(gmBase);

        SiVolume = OpenFST(siBase, siHeader, DiscKey);

        string tikPath = $"{gmIndex:x2}/title.tik";
        byte[]? tikData = ExtractFile(SiVolume, tikPath) ?? throw new InvalidDataException($"Could not find \"{tikPath}\" in the SI partition.");
        var ticket = TitleTicket.Parse(tikData);
        byte[] gmTitleKey = ticket.DecryptTitleKey();

        GmVolume = OpenFST(gmBase, gmHeader, gmTitleKey);
        GmPartitionIndex = gmIndex;
        GmPartitionName = names[gmIndex];
    }

    private readonly struct PartitionHeaderInfo(uint fstSize, uint fstSector, byte fstHashType)
    {
        public readonly uint FstSize = fstSize;
        public readonly uint FstSector = fstSector;
        public readonly byte FstHashType = fstHashType;
    }

    private PartitionHeaderInfo ReadPartitionHeader(long partitionBaseOffset)
    {
        var buf = new byte[0x60];

        if (_wud.ReadData(buf, partitionBaseOffset) != buf.Length)
            throw new InvalidDataException("Could not read partition header.");

        uint magic = BE32(buf, 0);

        if (magic != PartitionHeaderMagic)
            throw new InvalidDataException("Partition header signature mismatch.");

        uint fstSize = BE32(buf, 0x14);
        uint fstSector = BE32(buf, 0x18);
        byte fstHashType = buf[0x24];

        return new PartitionHeaderInfo(fstSize, fstSector, fstHashType);
    }

    private WuFstVolume OpenFST(long partitionBaseOffset, PartitionHeaderInfo header, byte[] partitionTitleKey)
    {
        long fstOffset = header.FstSector * DiscSectorSize;
        uint fstSizePadded = (header.FstSize + 15u) & ~15u;

        var fstData = new byte[fstSizePadded];

        if (_wud.ReadData(fstData, partitionBaseOffset + fstOffset) != fstData.Length)
            throw new InvalidDataException("Could not read FST data.");

        AesCbcDecryptInPlace(fstData, fstData.Length, partitionTitleKey, new byte[16]);

        uint magic = BE32(fstData, 0);

        if (magic != FstMagic)
            throw new InvalidDataException("FST signature mismatch — wrong key, or wrong fstSector/fstSize?");

        uint offsetFactor = BE32(fstData, 4);
        uint numCluster = BE32(fstData, 8);

        if (numCluster >= 0x1000)
            throw new InvalidDataException("FST cluster count out of range.");

        var volume = new WuFstVolume
        {
            PartitionBaseOffset = partitionBaseOffset,
            OffsetFactor = offsetFactor,
            PartitionTitleKey = partitionTitleKey,
        };

        int clusterTableOffset = 0x20;

        for (int i = 0; i < numCluster; i++)
        {
            int off = clusterTableOffset + i * 0x20;
            uint clusterOffsetUnits = BE32(fstData, off);
            uint clusterSizeUnits = BE32(fstData, off + 4);
            byte hashMode = fstData[off + 0x14];

            volume.Clusters.Add(new WuFstCluster
            {
                Offset = clusterOffsetUnits * DiscSectorSize,
                Size = clusterSizeUnits * DiscSectorSize,
                HashMode = hashMode,
            });
        }

        int fileTableOffset = clusterTableOffset + (int)numCluster * 0x20;

        if (fileTableOffset + 0x10 > fstData.Length)
            throw new InvalidDataException("FST file table is out of bounds.");

        uint rootTypeAndNameOffset = BE32(fstData, fileTableOffset);
        bool rootIsDir = ((rootTypeAndNameOffset >> 24) & 0x01) != 0;
        uint numFileEntries = BE32(fstData, fileTableOffset + 8);

        if (!rootIsDir || numFileEntries == 0 || fileTableOffset + (long)numFileEntries * 0x10 > fstData.Length)
            throw new InvalidDataException("FST root entry is invalid.");

        int nameTableOffset = fileTableOffset + (int)numFileEntries * 0x10;
        int nameTableLength = (int)header.FstSize - nameTableOffset;

        if (nameTableLength < 0) 
            nameTableLength = 0;

        for (int i = 0; i < numFileEntries; i++)
        {
            int entryOffset = fileTableOffset + i * 0x10;
            uint typeAndNameOffset = BE32(fstData, entryOffset);
            bool isDir = ((typeAndNameOffset >> 24) & 0x01) != 0;
            uint nameOffset = typeAndNameOffset & 0xFFFFFF;
            uint offsetField = BE32(fstData, entryOffset + 4);
            uint sizeField = BE32(fstData, entryOffset + 8);
            ushort clusterIndex = BinaryPrimitives.ReadUInt16BigEndian(fstData.AsSpan(entryOffset + 0xE, 2));
            string name = i == 0 ? string.Empty : ReadNullTerminatedAscii(fstData, nameTableOffset + (int)nameOffset, Math.Max(0, nameTableLength - (int)nameOffset));
            var entry = new WuFstEntry { IsDirectory = isDir, Name = name };

            if (isDir)
            {
                entry.ParentDirIndex = (int)offsetField;
                entry.DirEndIndex = (int)sizeField;
            }
            else
            {
                entry.FileOffsetField = offsetField;
                entry.FileSize = sizeField;
                entry.ClusterIndex = clusterIndex;
            }

            volume.Entries.Add(entry);
        }

        return volume;
    }

    private static int? FindEntryIndexByPath(WuFstVolume volume, string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        int currentIndex = 0;
        int searchStart = 1;
        int searchEnd = volume.Entries.Count > 0 ? volume.Entries[0].DirEndIndex : 0;

        foreach (var part in parts)
        {
            int? found = null;
            int idx = searchStart;

            while (idx < searchEnd)
            {
                if (string.Equals(volume.Entries[idx].Name, part, StringComparison.OrdinalIgnoreCase))
                {
                    found = idx;
                    break;
                }

                idx = volume.Entries[idx].IsDirectory ? volume.Entries[idx].DirEndIndex : idx + 1;
            }

            if (found is null) 
                return null;

            currentIndex = found.Value;

            if (!volume.Entries[currentIndex].IsDirectory)
            { 
                searchStart = searchEnd = currentIndex; 
                continue; 
            }

            searchStart = currentIndex + 1;
            searchEnd = volume.Entries[currentIndex].DirEndIndex;
        }

        return currentIndex;
    }

    public static IEnumerable<(string Path, int EntryIndex)> EnumerateFiles(WuFstVolume volume)
    {
        if (volume.Entries.Count == 0) 
            yield break;

        var pathStack = new Stack<(int EndIndex, string Path)>();

        pathStack.Push((volume.Entries[0].DirEndIndex, string.Empty));

        int i = 1;

        while (i < volume.Entries.Count)
        {
            while (pathStack.Count > 0 && i >= pathStack.Peek().EndIndex)
                pathStack.Pop();

            string parentPath = pathStack.Count > 0 ? pathStack.Peek().Path : string.Empty;
            var entry = volume.Entries[i];
            string fullPath = parentPath.Length == 0 ? entry.Name : $"{parentPath}/{entry.Name}";

            if (entry.IsDirectory)
            {
                pathStack.Push((entry.DirEndIndex, fullPath));
                i++;
            }
            else
            {
                yield return (fullPath, i);
                i++;
            }
        }
    }

    public byte[]? ExtractFile(WuFstVolume volume, string path)
    {
        var index = FindEntryIndexByPath(volume, path);

        if (index is null || volume.Entries[index.Value].IsDirectory) 
            return null;

        var entry = volume.Entries[index.Value];
        var buffer = new byte[entry.FileSize];

        ReadFile(volume, index.Value, 0, buffer, 0, buffer.Length);

        return buffer;
    }

    public void ExtractFileTo(WuFstVolume volume, int entryIndex, Stream destination)
    {
        var entry = volume.Entries[entryIndex];
        const int chunkSize = 1024 * 1024;
        var buffer = new byte[Math.Min(chunkSize, entry.FileSize == 0 ? 1 : entry.FileSize)];
        long offset = 0;

        while (offset < entry.FileSize)
        {
            int toRead = (int)Math.Min(buffer.Length, entry.FileSize - offset);
            int got = ReadFile(volume, entryIndex, offset, buffer, 0, toRead);

            if (got <= 0) 
                break;

            destination.Write(buffer, 0, got);
            offset += got;
        }
    }

    public Stream OpenFileStream(WuFstVolume volume, int entryIndex)
    {
        var entry = volume.Entries[entryIndex];

        if (entry.IsDirectory) 
            throw new InvalidOperationException("Cannot open a directory as a file stream.");

        return new DelegateReadStream(entry.FileSize, (pos, buf, off, cnt) => ReadFile(volume, entryIndex, pos, buf, off, cnt));
    }

    public int ReadFile(WuFstVolume volume, int entryIndex, long readOffset, byte[] dest, int destOffset, int size)
    {
        var entry = volume.Entries[entryIndex];
        var cluster = volume.Clusters[entry.ClusterIndex];

        return cluster.HashMode switch
        {
            2 => ReadFileHashed(volume, entry, cluster, readOffset, dest, destOffset, size),
            _ => ReadFileRaw(volume, entry, cluster, readOffset, dest, destOffset, size),
        };
    }

    private int ReadFileRaw(WuFstVolume volume, WuFstEntry entry, WuFstCluster cluster, long readOffset, byte[] dest, int destOffset, int size)
    {
        if (readOffset >= entry.FileSize) 
            return 0;

        long remaining = entry.FileSize - readOffset;
        int actualSize = (int)Math.Min(size, remaining);
        long absFileOffset = entry.FileOffsetField * volume.OffsetFactor + readOffset;
        int totalRead = 0;

        while (totalRead < actualSize)
        {
            long blockIndex = absFileOffset / WuFstVolume.SectorSize;
            long blockOffsetWithin = absFileOffset % WuFstVolume.SectorSize;
            byte[] block = GetDecryptedRawBlock(volume, entry.ClusterIndex, cluster, blockIndex);
            int copyLen = (int)Math.Min(actualSize - totalRead, WuFstVolume.SectorSize - blockOffsetWithin);

            Array.Copy(block, blockOffsetWithin, dest, destOffset + totalRead, copyLen);

            totalRead += copyLen;
            absFileOffset += copyLen;
        }

        return totalRead;
    }

    private byte[] GetDecryptedRawBlock(WuFstVolume volume, int clusterIndex, WuFstCluster cluster, long blockIndex)
    {
        long absolute = volume.PartitionBaseOffset + cluster.Offset + blockIndex * WuFstVolume.SectorSize;
        var block = new byte[WuFstVolume.SectorSize];

        if (_wud.ReadData(block, absolute) != block.Length)
            throw new InvalidDataException("Failed to read raw FST content block.");

        byte[] iv = new byte[16];

        if (blockIndex == 0)
        {
            iv[0] = (byte)(clusterIndex >> 8);
            iv[1] = (byte)(clusterIndex & 0xFF);
        }
        else
        {
            if (_wud.ReadData(iv, absolute - 16) != 16)
                throw new InvalidDataException("Failed to read IV for raw FST content block.");
        }

        AesCbcDecryptInPlace(block, block.Length, volume.PartitionTitleKey, iv);

        return block;
    }

    private int ReadFileHashed(WuFstVolume volume, WuFstEntry entry, WuFstCluster cluster, long readOffset, byte[] dest, int destOffset, int size)
    {
        const long BlockFileSize = 0xFC00;

        if (readOffset >= entry.FileSize) 
            return 0;

        long remaining = entry.FileSize - readOffset;
        int actualSize = (int)Math.Min(size, remaining);
        long fileReadOffset = entry.FileOffsetField * volume.OffsetFactor + readOffset;
        long blockIndex = fileReadOffset / BlockFileSize;
        long offsetWithinBlock = fileReadOffset % BlockFileSize;
        int totalRead = 0;

        while (totalRead < actualSize)
        {
            byte[] fileData = GetDecryptedHashedBlockFileData(volume, entry.ClusterIndex, cluster, blockIndex);
            int copyLen = (int)Math.Min(actualSize - totalRead, BlockFileSize - offsetWithinBlock);

            Array.Copy(fileData, offsetWithinBlock, dest, destOffset + totalRead, copyLen);

            totalRead += copyLen;
            blockIndex++;
            offsetWithinBlock = 0;
        }

        return totalRead;
    }

    private byte[] GetDecryptedHashedBlockFileData(WuFstVolume volume, int clusterIndex, WuFstCluster cluster, long blockIndex)
    {
        const int BlockSize = 0x10000;
        const int BlockHashSize = 0x400;
        const int BlockFileSize = 0xFC00;

        long absolute = volume.PartitionBaseOffset + cluster.Offset + blockIndex * BlockSize;
        var block = new byte[BlockSize];

        if (_wud.ReadData(block, absolute) != block.Length)
            throw new InvalidDataException("Failed to read hashed FST content block.");

        var hashPart = block.AsSpan(0, BlockHashSize).ToArray();

        AesCbcDecryptInPlace(hashPart, BlockHashSize, volume.PartitionTitleKey, new byte[16]);

        int h0Index = (int)(blockIndex % 16);
        var h0 = hashPart.AsSpan(h0Index * 20, 20).ToArray();
        var iv = h0.AsSpan(0, 16).ToArray();
        var fileData = block.AsSpan(BlockHashSize, BlockFileSize).ToArray();

        AesCbcDecryptInPlace(fileData, BlockFileSize, volume.PartitionTitleKey, iv);

        var computed = SHA1.HashData(fileData);

        if (!computed.AsSpan().SequenceEqual(h0.AsSpan(0, 20)))
            throw new InvalidDataException($"H0 hash mismatch in hashed content (cluster {clusterIndex}, block {blockIndex}) — corrupt dump or wrong key.");

        return fileData;
    }

    private static void AesCbcDecryptInPlace(byte[] data, int length, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();

        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();

        decryptor.TransformBlock(data, 0, length, data, 0);
    }

    private static uint BE32(byte[] buffer, int offset) => BinaryPrimitives.ReadUInt32BigEndian(buffer.AsSpan(offset, 4));

    private static string ReadNullTerminatedAscii(byte[] buffer, int offset, int maxLength)
    {
        if (offset < 0 || offset >= buffer.Length || maxLength <= 0) 
            return string.Empty;

        int len = 0;

        while (len < maxLength && offset + len < buffer.Length && buffer[offset + len] != 0) len++;

        return Encoding.ASCII.GetString(buffer, offset, len);
    }
}