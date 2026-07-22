using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
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

    public static bool LooksLikeWupFolder(string folderPath) => File.Exists(Path.Combine(folderPath, "title.tmd")) && File.Exists(Path.Combine(folderPath, "title.tik"));

    #region FST Parsing

    private readonly record struct FstClusterInfo(bool IsHashed);

    private readonly List<FstClusterInfo> _clusters = [];

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
            throw new InvalidOperationException($"'{path}'는 본편과 동일해서 이 업데이트 안에는 실제 데이터가 없습니다. 본편 쪽에서 읽어야 합니다.");

        return entry?.FileSize ?? 0;
    }

    public Stream OpenRead(string path)
    {
        var entry = FindEntry(path) ?? throw new FileNotFoundException($"WUP 안에서 파일을 찾을 수 없습니다: {path}");

        if (entry.IsSharedWithBase)
            throw new InvalidOperationException($"'{path}'는 본편과 동일해서 이 업데이트 안에는 실제 데이터가 없습니다. 본편 쪽에서 읽어야 합니다.");

        return new FstEntryStream(this, entry, path);
    }

    /// <summary>
    /// Lazily reads an FstEntry's bytes on demand instead of eagerly allocating a single
    /// entry.FileSize-sized byte[] up front (the previous OpenRead did this, which both wasted
    /// memory for every read and outright crashed - via an OverflowException converting the size
    /// to an array length - for any content file over ~2GB, since entry.FileSize is a uint that no
    /// longer fits as a single array's length past that point).
    /// </summary>
    private sealed class FstEntryStream(WupTitleSource owner, FstEntry entry, string path) : Stream
    {
        private long _position;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => entry.FileSize;

        public override long Position
        {
            get => _position;
            set => _position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long remaining = entry.FileSize - _position;
            if (remaining <= 0)
                return 0;

            int toRead = (int)Math.Min(count, remaining);

            try
            {
                owner.ReadFileEntry(entry, _position, buffer, offset, toRead);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"'{path}' 읽기 실패 (clusterIndex={entry.ClusterIndex}, offsetField={entry.FileOffsetField}, " + $"byteOffset={(long)entry.FileOffsetField * owner._fstOffsetFactor}, size={entry.FileSize}): {ex.Message}", ex);
            }

            _position += toRead;
            return toRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            _position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => entry.FileSize + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin)),
            };
            return _position;
        }

        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
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

    private WupContent FindContent(int index) => _contents.FirstOrDefault(c => c.Index == index) ?? throw new InvalidDataException($"TMD에 콘텐츠 인덱스 {index}가 없습니다.");

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
            throw new InvalidDataException("해시트리 콘텐츠 블록의 헤더(0x400바이트)조차 읽지 못했습니다 — 파일이 잘렸거나 오프셋 계산이 잘못됐을 수 있습니다.");

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

    #endregion

    public void Dispose()
    {
        foreach (var s in _hashedStreams.Values)
            s.Dispose();

        _hashedStreams.Clear();
        _rawContentCache.Clear();
    }
}