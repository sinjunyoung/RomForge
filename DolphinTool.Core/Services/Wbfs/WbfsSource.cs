using System.Buffers.Binary;
using DolphinTool.Core.Rvz;
using Microsoft.Win32.SafeHandles;

namespace DolphinTool.Core.Services.Wbfs;

internal sealed class WbfsSource : IRvzInputSource
{
    private const uint Magic = 0x53464257;
    private const int HeaderSize = 512;
    private const long WiiSectorSize = 0x8000;
    private const long WiiSectorCount = 143432 * 2;
    private const long WiiSingleLayerSize = 4699979776;
    private const int WiiDiscHeaderSize = 256;

    private readonly record struct FileEntry(SafeFileHandle Handle, long BaseAddress, long Size);

    private readonly List<FileEntry> _files = [];
    private readonly long _wbfsSectorSize;
    private readonly int _wbfsSectorShift;
    private readonly long _blocksPerDisc;
    private readonly ushort[] _wlbaTable;
    private readonly long _length;

    private WbfsSource(List<FileEntry> files, long wbfsSectorSize, int wbfsSectorShift, long blocksPerDisc, ushort[] wlbaTable, long length)
    {
        _files = files;
        _wbfsSectorSize = wbfsSectorSize;
        _wbfsSectorShift = wbfsSectorShift;
        _blocksPerDisc = blocksPerDisc;
        _wlbaTable = wlbaTable;
        _length = length;
    }

    public long Length => _length;

    public static bool IsWbfs(SafeFileHandle handle)
    {
        if (RandomAccess.GetLength(handle) < HeaderSize)
            return false;

        Span<byte> magic = stackalloc byte[4];

        RvzIo.ReadExactly(handle, magic, 0);

        return BinaryPrimitives.ReadUInt32LittleEndian(magic) == Magic;
    }

    public static WbfsSource Open(string path, SafeFileHandle primaryHandle)
    {
        var files = new List<FileEntry>
        {
            new(primaryHandle, 0, RandomAccess.GetLength(primaryHandle))
        };

        try
        {
            long totalSize = files[0].Size;

            for (int i = 1; i < 10; i++)
            {
                string siblingPath = ReplaceLastChar(path, (char)('0' + i));

                if (!File.Exists(siblingPath))
                    break;

                var handle = File.OpenHandle(siblingPath, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);
                long size = RandomAccess.GetLength(handle);

                files.Add(new FileEntry(handle, totalSize, size));

                totalSize += size;
            }

            byte[] header = new byte[HeaderSize];

            RvzIo.ReadExactly(primaryHandle, header, 0);

            if (BinaryPrimitives.ReadUInt32LittleEndian(header) != Magic)
                throw new InvalidDataException("WBFS 파일이 아닙니다.");

            uint hdSectorCount = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(4));
            int hdSectorShift = header[8];
            int wbfsSectorShift = header[9];

            if (hdSectorShift is < 0 or > 62)
                throw new InvalidDataException("WBFS 헤더가 올바르지 않습니다.");

            long hdSectorSize = 1L << hdSectorShift;

            if (totalSize != checked((long)hdSectorCount * hdSectorSize))
                throw new InvalidDataException("WBFS 파일 크기가 헤더와 다릅니다. 파일이 잘렸을 수 있습니다.");

            if (wbfsSectorShift < hdSectorShift || wbfsSectorShift > 62)
                throw new InvalidDataException("WBFS 헤더가 올바르지 않습니다.");

            long wbfsSectorSize = 1L << wbfsSectorShift;

            if (wbfsSectorSize < WiiSectorSize)
                throw new InvalidDataException("WBFS 섹터 크기가 올바르지 않습니다.");

            int maxDisc = checked((int)Math.Min(hdSectorSize - 12, HeaderSize - 12));
            int discIndex = -1;

            for (int i = 0; i < maxDisc; i++)
            {
                if (header[12 + i] != 0)
                {
                    discIndex = i;
                    break;
                }
            }

            if (discIndex < 0)
                throw new InvalidDataException("WBFS 파일에 디스크가 없습니다.");

            int wbfsToWiiShift = wbfsSectorShift - 15;

            if (wbfsToWiiShift < 0 || wbfsToWiiShift > 62)
                throw new InvalidDataException("WBFS 섹터 크기가 올바르지 않습니다.");

            long blocksPerDisc = WiiSectorCount >> wbfsToWiiShift;

            if (blocksPerDisc <= 0 || blocksPerDisc > ushort.MaxValue)
                throw new InvalidDataException("WBFS 블록 수가 올바르지 않습니다.");

            long discInfoSizeRaw = WiiDiscHeaderSize + checked(blocksPerDisc * 2);
            long discInfoSize = (discInfoSizeRaw + hdSectorSize - 1) / hdSectorSize * hdSectorSize;
            long wlbaOffset = checked(hdSectorSize + discIndex * discInfoSize + WiiDiscHeaderSize);
            long wlbaSize = checked(blocksPerDisc * 2);

            if (wlbaOffset < 0 || wlbaOffset + wlbaSize > totalSize)
                throw new InvalidDataException("WBFS WLBA 테이블이 파일 범위를 벗어났습니다.");

            byte[] wlbaBytes = new byte[checked((int)wlbaSize)];

            ReadVirtual(files, wlbaBytes, wlbaOffset);

            var wlbaTable = new ushort[checked((int)blocksPerDisc)];

            for (int i = 0; i < wlbaTable.Length; i++)
                wlbaTable[i] = BinaryPrimitives.ReadUInt16BigEndian(wlbaBytes.AsSpan(i * 2, 2));

            long lastUsedBlock = 0;

            for (long i = 0; i < blocksPerDisc; i++)
            {
                if (wlbaTable[i] != 0)
                    lastUsedBlock = i + 1;
            }

            long length = Math.Max(WiiSingleLayerSize, checked(lastUsedBlock * wbfsSectorSize));

            return new WbfsSource(files, wbfsSectorSize, wbfsSectorShift, blocksPerDisc, wlbaTable, length);
        }
        catch
        {
            for (int i = 1; i < files.Count; i++)
                files[i].Handle.Dispose();

            throw;
        }
    }

    private static string ReplaceLastChar(string path, char replacement) => path.Length == 0 ? path : path[..^1] + replacement;

    private static void ReadVirtual(List<FileEntry> files, Span<byte> destination, long offset)
    {
        int written = 0;

        while (written < destination.Length)
        {
            long currentOffset = offset + written;

            FileEntry? selected = null;

            foreach (var file in files)
            {
                if (currentOffset >= file.BaseAddress && currentOffset < file.BaseAddress + file.Size)
                {
                    selected = file;
                    break;
                }
            }

            if (selected is not FileEntry entry)
                throw new EndOfStreamException("WBFS 파일 범위를 벗어난 읽기입니다.");

            long fileOffset = currentOffset - entry.BaseAddress;
            int chunk = (int)Math.Min(entry.Size - fileOffset, destination.Length - written);

            RvzIo.ReadExactly(entry.Handle, destination.Slice(written, chunk), fileOffset);

            written += chunk;
        }
    }

    public void Read(long offset, Span<byte> destination)
    {
        if (offset < 0 || destination.Length > Length - offset)
            throw new EndOfStreamException("WBFS 범위를 벗어난 읽기입니다.");

        int written = 0;

        while (written < destination.Length)
        {
            long currentOffset = offset + written;
            long baseCluster = currentOffset >> _wbfsSectorShift;

            if (baseCluster >= _blocksPerDisc)
                throw new EndOfStreamException("WBFS 디스크 범위를 벗어난 읽기입니다.");

            long clusterOffset = currentOffset & (_wbfsSectorSize - 1);
            int chunk = (int)Math.Min(_wbfsSectorSize - clusterOffset, destination.Length - written);
            ushort wlba = _wlbaTable[baseCluster];

            if (wlba == 0)
            {
                destination
                    .Slice(written, chunk)
                    .Clear();

                written += chunk;
                continue;
            }

            long finalAddress = checked((long)wlba * _wbfsSectorSize + clusterOffset);
            bool found = false;

            foreach (var entry in _files)
            {
                if (finalAddress < entry.BaseAddress || finalAddress >= entry.BaseAddress + entry.Size)
                    continue;

                long fileOffset = finalAddress - entry.BaseAddress;
                long tillEndOfFile = entry.Size - fileOffset;
                int actualChunk = (int)Math.Min(Math.Min(tillEndOfFile, _wbfsSectorSize - clusterOffset), destination.Length - written);

                if (actualChunk <= 0)
                    throw new EndOfStreamException("WBFS 파일이 예상보다 일찍 끝났습니다.");

                RvzIo.ReadExactly(entry.Handle, destination.Slice( written, actualChunk), fileOffset);

                written += actualChunk;
                found = true;
                break;
            }

            if (!found)
            {
                throw new InvalidDataException("WBFS 클러스터 위치가 파일 범위를 벗어났습니다.");
            }
        }
    }

    public void Dispose()
    {
        foreach (var entry in _files)
            entry.Handle.Dispose();
    }
}