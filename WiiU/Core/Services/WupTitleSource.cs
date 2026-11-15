using System.Buffers.Binary;
using Common.Localization;
using System.Security.Cryptography;
using System.Text;
using WiiU.Core.IO;
using WiiU.Core.Models;

namespace WiiU.Core.Services;

public sealed class WupTitleSource : ITitleSource
{
    private const int RawFullDecryptSizeLimit = 64 * 1024 * 1024;
    private const int HashedBlockSize = 0x10000;
    private const int HashedHeaderSize = 0x400;
    private const int HashedDataSize = 0xFC00;

    private readonly int _fstOffsetFactor;
    private readonly byte[] _titleKey;
    private readonly string _folder;
    private readonly List<WupContent> _contents;
    private readonly List<FstEntry> _entries = [];
    private readonly Dictionary<int, byte[]> _rawContentCache = [];
    private readonly Dictionary<int, FileStream> _hashedStreams = [];

    public string TitleIdHex { get; }

    public int TitleVersion { get; }

    public int FstOffsetFactor => _fstOffsetFactor;

    public WupTitleSource(string folderPath)
    {
        _folder = folderPath;

        string tmdPath = Path.Combine(folderPath, "title.tmd");
        string tikPath = Path.Combine(folderPath, "title.tik");

        if (!File.Exists(tmdPath))
            throw new FileNotFoundException(Loc.S.ErrTitleTmdNotFound, tmdPath);

        if (!File.Exists(tikPath))
            throw new FileNotFoundException(Loc.S.ErrTitleTikNotFound, tikPath);

        var tmdBytes = File.ReadAllBytes(tmdPath);
        var (titleIdHex, titleVersion, contents) = WupTmd.Parse(tmdBytes);

        TitleIdHex = titleIdHex;
        TitleVersion = titleVersion;
        _contents = contents;

        var tikBytes = File.ReadAllBytes(tikPath);
        var ticket = TitleTicket.Parse(tikBytes);

        _titleKey = ticket.DecryptTitleKey();

        var fstContent = _contents.FirstOrDefault(c => c.Index == 0) ?? throw new InvalidDataException(Loc.S.ErrTmdFstContentNotFound);
        byte[] fstData = DecryptRawContentFull(fstContent);

        _fstOffsetFactor = ParseFst(fstData);
    }

    public static bool LooksLikeWupFolder(string folderPath) => File.Exists(Path.Combine(folderPath, "title.tmd")) && File.Exists(Path.Combine(folderPath, "title.tik"));

    private readonly record struct FstClusterInfo(bool IsHashed);

    private readonly List<FstClusterInfo> _clusters = [];

    private int ParseFst(byte[] fst)
    {
        uint magic = BinaryPrimitives.ReadUInt32BigEndian(fst.AsSpan(0, 4));

        if (magic != 0x46535400)
            throw new InvalidDataException(Loc.S.ErrContent0NotFst);

        uint offsetFactor = BinaryPrimitives.ReadUInt32BigEndian(fst.AsSpan(4, 4));
        uint numCluster = BinaryPrimitives.ReadUInt32BigEndian(fst.AsSpan(8, 4));

        if (numCluster > 4096)
            throw new InvalidDataException(Loc.S.ErrFstClusterCountAbnormal);

        int clusterTableOffset = 0x20;

        for (int i = 0; i < numCluster; i++)
        {
            int off = clusterTableOffset + i * 0x20;
            byte hashMode = fst[off + 0x14];

            _clusters.Add(new FstClusterInfo(IsHashed: hashMode == 2));
        }

        int fileTableOffset = clusterTableOffset + (int)numCluster * 0x20;
        uint numFileEntries = BinaryPrimitives.ReadUInt32BigEndian(fst.AsSpan(fileTableOffset + 8, 4));
        int nameTableOffset = fileTableOffset + (int)numFileEntries * 0x10;

        for (int i = 0; i < numFileEntries; i++)
        {
            int eoff = fileTableOffset + i * 0x10;
            uint typeAndNameOffset = BinaryPrimitives.ReadUInt32BigEndian(fst.AsSpan(eoff, 4));
            uint offsetField = BinaryPrimitives.ReadUInt32BigEndian(fst.AsSpan(eoff + 4, 4));
            uint sizeField = BinaryPrimitives.ReadUInt32BigEndian(fst.AsSpan(eoff + 8, 4));
            ushort clusterIndex = BinaryPrimitives.ReadUInt16BigEndian(fst.AsSpan(eoff + 0xE, 2));
            bool isDir = ((typeAndNameOffset >> 24) & 0x01) != 0;
            bool isSharedWithBase = (typeAndNameOffset & 0x80000000) != 0;
            uint nameOffset = typeAndNameOffset & 0xFFFFFF;
            string name = i == 0 ? string.Empty : ReadCString(fst, nameTableOffset + (int)nameOffset);
            var entry = new FstEntry { IsDirectory = isDir, Name = name, ClusterIndex = clusterIndex, IsSharedWithBase = isSharedWithBase };

            if (isDir)
            {
                entry.ParentDirIndex = (int)offsetField;
                entry.DirEndIndex = (int)sizeField;
            }
            else
            {
                entry.FileOffsetField = offsetField;
                entry.FileSize = sizeField;
            }

            _entries.Add(entry);
        }

        return (int)offsetFactor;
    }

    private static string ReadCString(byte[] data, int offset)
    {
        if (offset < 0 || offset >= data.Length)
            return string.Empty;

        int end = offset;

        while (end < data.Length && data[end] != 0)
            end++;

        return Encoding.UTF8.GetString(data, offset, end - offset);
    }

    public IEnumerable<string> EnumerateFiles()
    {
        if (_entries.Count == 0)
            yield break;

        var pathStack = new Stack<(int EndIndex, string Path)>();

        pathStack.Push((_entries[0].DirEndIndex, string.Empty));

        int i = 1;

        while (i < _entries.Count)
        {
            while (pathStack.Count > 0 && i >= pathStack.Peek().EndIndex)
                pathStack.Pop();

            string parentPath = pathStack.Count > 0 ? pathStack.Peek().Path : string.Empty;
            var entry = _entries[i];
            string fullPath = parentPath.Length == 0 ? entry.Name : $"{parentPath}/{entry.Name}";

            if (entry.IsDirectory)
            {
                pathStack.Push((entry.DirEndIndex, fullPath));
                i++;
            }
            else
            {
                if (!entry.IsSharedWithBase)
                    yield return fullPath;

                i++;
            }
        }
    }

    public long GetFileSize(string path)
    {
        var entry = FindEntry(path);

        if (entry is not null && entry.IsSharedWithBase)
            throw new InvalidOperationException(Loc.S.ErrUpdateSameAsBase(path));

        return entry?.FileSize ?? 0;
    }

    public Stream OpenRead(string path)
    {
        var entry = FindEntry(path) ?? throw new FileNotFoundException(Loc.S.ErrFileNotFoundInWup(path));

        if (entry.IsSharedWithBase)
            throw new InvalidOperationException(Loc.S.ErrUpdateSameAsBase(path));

        return new FstEntryStream(this, entry, path);
    }

    private FstEntry? FindEntry(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        int currentIndex = 0;
        int searchStart = 1;
        int searchEnd = _entries.Count > 0 ? _entries[0].DirEndIndex : 0;

        foreach (var part in parts)
        {
            int? found = null;
            int idx = searchStart;

            while (idx < searchEnd)
            {
                if (string.Equals(_entries[idx].Name, part, StringComparison.Ordinal))
                {
                    found = idx;
                    break;
                }

                idx = _entries[idx].IsDirectory ? _entries[idx].DirEndIndex : idx + 1;
            }

            if (found is null)
                return null;

            currentIndex = found.Value;

            if (!_entries[currentIndex].IsDirectory)
            {
                searchStart = searchEnd = currentIndex;
                continue;
            }

            searchStart = currentIndex + 1;
            searchEnd = _entries[currentIndex].DirEndIndex;
        }

        return _entries[currentIndex];
    }

    internal void ReadFileEntry(FstEntry entry, long readOffset, byte[] dest, int destOffset, int size)
    {
        int clusterIndex = entry.ClusterIndex;
        long baseOffset = (long)entry.FileOffsetField * FstOffsetFactor + readOffset;
        bool hashed = clusterIndex < _clusters.Count && _clusters[clusterIndex].IsHashed;

        if (hashed)
            ReadHashedRange(clusterIndex, baseOffset, dest, destOffset, size);
        else
            ReadRawRange(clusterIndex, baseOffset, dest, destOffset, size);
    }

    private WupContent FindContent(int index) => _contents.FirstOrDefault(c => c.Index == index) ?? throw new InvalidDataException(Loc.S.ErrTmdContentIndexMissing(index));

    private void ReadRawRange(int clusterIndex, long offset, byte[] dest, int destOffset, int size)
    {
        if (!_rawContentCache.TryGetValue(clusterIndex, out var decrypted))
        {
            var content = FindContent(clusterIndex);

            decrypted = DecryptRawContentFull(content);
            _rawContentCache[clusterIndex] = decrypted;
        }

        if (offset + size > decrypted.Length)
            throw new InvalidDataException(Loc.S.ErrContentReadOutOfRange(clusterIndex));

        Array.Copy(decrypted, offset, dest, destOffset, size);
    }

    private byte[] DecryptRawContentFull(WupContent content)
    {
        string appPath = Path.Combine(_folder, $"{content.CIDHex}.app");

        if (!File.Exists(appPath))
            throw new FileNotFoundException(Loc.S.ErrContentFileNotFound(appPath), appPath);

        var fi = new FileInfo(appPath);

        if (fi.Length > RawFullDecryptSizeLimit)
            throw new InvalidDataException(Loc.S.ErrRawContentAbnormallyLarge(content.CIDHex, fi.Length));

        byte[] cipherData = File.ReadAllBytes(appPath);
        byte[] iv = new byte[16];

        iv[0] = (byte)(content.Index >> 8);
        iv[1] = (byte)(content.Index & 0xFF);

        AesCbcDecryptInPlace(cipherData, cipherData.Length, _titleKey, iv);

        return cipherData;
    }

    private void ReadHashedRange(int clusterIndex, long offset, byte[] dest, int destOffset, int size)
    {
        if (!_hashedStreams.TryGetValue(clusterIndex, out var stream))
        {
            var content = FindContent(clusterIndex);
            string appPath = Path.Combine(_folder, $"{content.CIDHex}.app");

            if (!File.Exists(appPath))
                throw new FileNotFoundException(Loc.S.ErrContentFileNotFound(appPath), appPath);

            stream = File.OpenRead(appPath);
            _hashedStreams[clusterIndex] = stream;
        }

        int totalRead = 0;

        while (totalRead < size)
        {
            long blockIndex = offset / HashedDataSize;
            long offsetWithinBlock = offset % HashedDataSize;
            byte[] blockData = GetDecryptedHashedDataBlock(stream, blockIndex, clusterIndex);
            int copyLen = (int)Math.Min(size - totalRead, HashedDataSize - offsetWithinBlock);

            Array.Copy(blockData, offsetWithinBlock, dest, destOffset + totalRead, copyLen);

            totalRead += copyLen;
            offset += copyLen;
        }
    }

    private byte[] GetDecryptedHashedDataBlock(FileStream stream, long blockIndex, int contentIndex)
    {
        long absolute = blockIndex * HashedBlockSize;
        var block = new byte[HashedBlockSize];

        stream.Position = absolute;

        int got = ReadFully(stream, block, HashedBlockSize);

        if (got < HashedHeaderSize)
            throw new InvalidDataException(Loc.S.ErrHashTreeBlockHeaderUnreadable);

        var hashPart = block.AsSpan(0, HashedHeaderSize).ToArray();

        byte[] headerIv = new byte[16];

        headerIv[0] = (byte)(contentIndex >> 8);
        headerIv[1] = (byte)(contentIndex & 0xFF);

        AesCbcDecryptInPlace(hashPart, HashedHeaderSize, _titleKey, headerIv);

        int h0Index = (int)(blockIndex % 16);
        var h0 = hashPart.AsSpan(h0Index * 20, 20).ToArray();
        var iv = h0.AsSpan(0, 16).ToArray();

        if (h0Index == 0)
            iv[1] ^= (byte)contentIndex;

        var dataPart = block.AsSpan(HashedHeaderSize, HashedDataSize).ToArray();
        int dataAvailable = Math.Max(0, got - HashedHeaderSize);
        int alignedLen = dataAvailable - (dataAvailable % 16);

        if (alignedLen > 0)
            AesCbcDecryptInPlace(dataPart, alignedLen, _titleKey, iv);

        return dataPart;
    }

    private static int ReadFully(Stream stream, byte[] buffer, int count)
    {
        int total = 0;

        while (total < count)
        {
            int got = stream.Read(buffer, total, count - total);

            if (got == 0)
                break;

            total += got;
        }

        return total;
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

    public void Dispose()
    {
        foreach (var s in _hashedStreams.Values)
            s.Dispose();

        _hashedStreams.Clear();
        _rawContentCache.Clear();
    }
}