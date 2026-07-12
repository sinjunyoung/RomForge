using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace WiiU.Core.Services;

public sealed class WupTitleSource : ITitleSource
{
    private const int RawFullDecryptSizeLimit = 64 * 1024 * 1024;

    private const int HashedBlockSize = 0x10000;
    private const int HashedHeaderSize = 0x400;
    private const int HashedDataSize = 0xFC00;

    private readonly string _folder;
    private readonly byte[] _titleKey;
    private readonly List<WupContent> _contents;
    private readonly List<FstEntry> _entries = [];
    private readonly int _fstOffsetFactor;

    private readonly Dictionary<int, byte[]> _rawContentCache = [];
    private readonly Dictionary<int, FileStream> _hashedStreams = [];

    public string TitleIdHex { get; }

    public int TitleVersion { get; }

    public static bool LooksLikeWupFolder(string folderPath) => File.Exists(Path.Combine(folderPath, "title.tmd")) && File.Exists(Path.Combine(folderPath, "title.tik"));

    public WupTitleSource(string folderPath)
    {
        _folder = folderPath;

        string tmdPath = Path.Combine(folderPath, "title.tmd");
        string tikPath = Path.Combine(folderPath, "title.tik");

        if (!File.Exists(tmdPath)) 
            throw new FileNotFoundException("title.tmd를 찾을 수 없습니다.", tmdPath);

        if (!File.Exists(tikPath)) 
            throw new FileNotFoundException("title.tik를 찾을 수 없습니다.", tikPath);

        var tmdBytes = File.ReadAllBytes(tmdPath);
        var (titleIdHex, titleVersion, contents) = WupTmd.Parse(tmdBytes);

        TitleIdHex = titleIdHex;
        TitleVersion = titleVersion;
        _contents = contents;

        var tikBytes = File.ReadAllBytes(tikPath);
        var ticket = TitleTicket.Parse(tikBytes);

        _titleKey = ticket.DecryptTitleKey();

        var fstContent = _contents.FirstOrDefault(c => c.Index == 0) ?? throw new InvalidDataException("TMD에서 index 0(FST) 콘텐츠를 찾을 수 없습니다.");
        byte[] fstData = DecryptRawContentFull(fstContent);

        _fstOffsetFactor = ParseFst(fstData);
    }

    #region FST Parsing

    private readonly record struct FstClusterInfo(bool IsHashed);

    private List<FstClusterInfo> _clusters = [];

    private sealed class FstEntry
    {
        public bool IsDirectory;
        public string Name = "";
        public int ParentDirIndex;
        public int DirEndIndex;
        public uint FileOffsetField;
        public uint FileSize;
        public ushort ClusterIndex;
    }

    private int ParseFst(byte[] fst)
    {
        uint magic = BinaryPrimitives.ReadUInt32BigEndian(fst.AsSpan(0, 4));

        if (magic != 0x46535400)
            throw new InvalidDataException("content 0이 FST가 아닙니다 (매직 불일치) — WUP 폴더 구조가 예상과 다릅니다.");

        uint offsetFactor = BinaryPrimitives.ReadUInt32BigEndian(fst.AsSpan(4, 4));
        uint numCluster = BinaryPrimitives.ReadUInt32BigEndian(fst.AsSpan(8, 4));

        if (numCluster > 4096)
            throw new InvalidDataException("FST 클러스터 개수가 비정상적으로 많습니다.");

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
            uint nameOffset = typeAndNameOffset & 0xFFFFFF;
            string name = i == 0 ? string.Empty : ReadCString(fst, nameTableOffset + (int)nameOffset);
            var entry = new FstEntry { IsDirectory = isDir, Name = name, ClusterIndex = clusterIndex };

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

    #endregion

    #region ITitleSource

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
                yield return fullPath;
                i++;
            }
        }
    }

    public long GetFileSize(string path)
    {
        var entry = FindEntry(path);

        return entry?.FileSize ?? 0;
    }

    public Stream OpenRead(string path)
    {
        var entry = FindEntry(path) ?? throw new FileNotFoundException($"WUP 안에서 파일을 찾을 수 없습니다: {path}");

        var buffer = new byte[entry.FileSize];

        ReadFileEntry(entry, 0, buffer, 0, buffer.Length);

        return new MemoryStream(buffer, writable: false);
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
                if (string.Equals(_entries[idx].Name, part, StringComparison.OrdinalIgnoreCase))
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

    #endregion

    #region Content Reading

    private void ReadFileEntry(FstEntry entry, long readOffset, byte[] dest, int destOffset, int size)
    {
        int clusterIndex = entry.ClusterIndex;
        long baseOffset = (long)entry.FileOffsetField * _fstOffsetFactor + readOffset;
        bool hashed = clusterIndex < _clusters.Count && _clusters[clusterIndex].IsHashed;

        if (hashed)
            ReadHashedRange(clusterIndex, baseOffset, dest, destOffset, size);
        else
            ReadRawRange(clusterIndex, baseOffset, dest, destOffset, size);
    }

    private WupContent FindContent(int index) =>
        _contents.FirstOrDefault(c => c.Index == index)
            ?? throw new InvalidDataException($"TMD에 콘텐츠 인덱스 {index}가 없습니다.");

    private void ReadRawRange(int clusterIndex, long offset, byte[] dest, int destOffset, int size)
    {
        if (!_rawContentCache.TryGetValue(clusterIndex, out var decrypted))
        {
            var content = FindContent(clusterIndex);

            decrypted = DecryptRawContentFull(content);
            _rawContentCache[clusterIndex] = decrypted;
        }

        if (offset + size > decrypted.Length)
            throw new InvalidDataException($"콘텐츠 {clusterIndex}에서 범위를 벗어난 읽기 요청입니다.");

        Array.Copy(decrypted, offset, dest, destOffset, size);
    }

    private byte[] DecryptRawContentFull(WupContent content)
    {
        string appPath = Path.Combine(_folder, $"{content.CIDHex}.app");

        if (!File.Exists(appPath))
            throw new FileNotFoundException($"콘텐츠 파일을 찾을 수 없습니다: {appPath}", appPath);

        var fi = new FileInfo(appPath);

        if (fi.Length > RawFullDecryptSizeLimit)
            throw new InvalidDataException($"콘텐츠 {content.CIDHex}.app가 raw 타입인데 비정상적으로 큽니다 ({fi.Length} bytes) — 해시트리 타입 판별이 잘못됐을 수 있습니다.");

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
                throw new FileNotFoundException($"콘텐츠 파일을 찾을 수 없습니다: {appPath}", appPath);

            stream = File.OpenRead(appPath);
            _hashedStreams[clusterIndex] = stream;
        }

        int totalRead = 0;

        while (totalRead < size)
        {
            long blockIndex = offset / HashedDataSize;
            long offsetWithinBlock = offset % HashedDataSize;

            byte[] blockData = GetDecryptedHashedDataBlock(stream, blockIndex);

            int copyLen = (int)Math.Min(size - totalRead, HashedDataSize - offsetWithinBlock);

            Array.Copy(blockData, offsetWithinBlock, dest, destOffset + totalRead, copyLen);

            totalRead += copyLen;
            offset += copyLen;
        }
    }

    private byte[] GetDecryptedHashedDataBlock(FileStream stream, long blockIndex)
    {
        long absolute = blockIndex * HashedBlockSize;
        var block = new byte[HashedBlockSize];

        stream.Position = absolute;

        int got = stream.Read(block, 0, HashedBlockSize);

        if (got != HashedBlockSize)
            throw new InvalidDataException("해시트리 콘텐츠 블록을 온전히 읽지 못했습니다 (파일이 잘렸을 수 있습니다).");

        var hashPart = block.AsSpan(0, HashedHeaderSize).ToArray();

        AesCbcDecryptInPlace(hashPart, HashedHeaderSize, _titleKey, new byte[16]);

        int h0Index = (int)(blockIndex % 16);
        var h0 = hashPart.AsSpan(h0Index * 20, 20).ToArray();
        var iv = h0.AsSpan(0, 16).ToArray();

        var dataPart = block.AsSpan(HashedHeaderSize, HashedDataSize).ToArray();

        AesCbcDecryptInPlace(dataPart, HashedDataSize, _titleKey, iv);

        return dataPart;
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

    #endregion

    public void Dispose()
    {
        foreach (var s in _hashedStreams.Values)
            s.Dispose();

        _hashedStreams.Clear();
        _rawContentCache.Clear();
    }
}

public sealed record WupContent(uint ContentId, ushort Index, ushort Type, ulong Size, byte[] Hash)
{
    public bool IsHashed => (Type & 0x0002) != 0;

    public string CIDHex => ContentId.ToString("x8");
}

internal static class WupTmd
{
    public static (string TitleIdHex, int TitleVersion, List<WupContent> Contents) Parse(byte[] tmd)
    {
        uint sigType = BinaryPrimitives.ReadUInt32BigEndian(tmd.AsSpan(0, 4));

        int bodyStart = sigType switch
        {
            0x00010000 or 0x00010003 => 0x240,
            0x00010001 or 0x00010004 => 0x140,
            0x00010002 or 0x00010005 => 0x080,
            _ => throw new InvalidDataException($"지원하지 않는 TMD 서명 타입: 0x{sigType:X8}"),
        };

        ulong titleId = BinaryPrimitives.ReadUInt64BigEndian(tmd.AsSpan(bodyStart + 0x4C, 8));
        ushort titleVersion = BinaryPrimitives.ReadUInt16BigEndian(tmd.AsSpan(bodyStart + 0x9C, 2));
        ushort numContents = BinaryPrimitives.ReadUInt16BigEndian(tmd.AsSpan(bodyStart + 0x9E, 2));
        int contentInfoTableOffset = bodyStart + 0xC4;
        int contentTableOffset = contentInfoTableOffset + 64 * 36;
        var contents = new List<WupContent>(numContents);

        for (int i = 0; i < numContents; i++)
        {
            int off = contentTableOffset + i * 48;
            uint cid = BinaryPrimitives.ReadUInt32BigEndian(tmd.AsSpan(off, 4));
            ushort index = BinaryPrimitives.ReadUInt16BigEndian(tmd.AsSpan(off + 4, 2));
            ushort type = BinaryPrimitives.ReadUInt16BigEndian(tmd.AsSpan(off + 6, 2));
            ulong size = BinaryPrimitives.ReadUInt64BigEndian(tmd.AsSpan(off + 8, 8));
            byte[] hash = tmd.AsSpan(off + 16, 32).ToArray();

            contents.Add(new WupContent(cid, index, type, size, hash));
        }

        return (titleId.ToString("x16"), titleVersion, contents);
    }
}