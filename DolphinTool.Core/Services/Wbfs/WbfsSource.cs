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
    private const int WiiDiscHeaderSize = 256;

    private readonly record struct FileEntry(SafeFileHandle Handle, long BaseAddress, long Size);

    private readonly List<FileEntry> _files = [];
    private readonly long _hdSectorSize;
    private readonly long _wbfsSectorSize;
    private readonly int _wbfsSectorShift;
    private readonly long _blocksPerDisc;
    private readonly ushort[] _wlbaTable;

    private WbfsSource(List<FileEntry> files, long hdSectorSize, long wbfsSectorSize, int wbfsSectorShift,
        long blocksPerDisc, ushort[] wlbaTable)
    {
        _files = files;
        _hdSectorSize = hdSectorSize;
        _wbfsSectorSize = wbfsSectorSize;
        _wbfsSectorShift = wbfsSectorShift;
        _blocksPerDisc = blocksPerDisc;
        _wlbaTable = wlbaTable;
    }

    public long Length => WiiSectorCount * WiiSectorSize;

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
        var files = new List<FileEntry> { new(primaryHandle, 0, RandomAccess.GetLength(primaryHandle)) };

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

            if (totalSize != hdSectorCount * hdSectorSize)
                throw new InvalidDataException("WBFS 파일 크기가 헤더와 다릅니다. 파일이 잘렸을 수 있습니다.");

            if (wbfsSectorShift is < 0 or > 62)
                throw new InvalidDataException("WBFS 헤더가 올바르지 않습니다.");

            long wbfsSectorSize = 1L << wbfsSectorShift;

            if (wbfsSectorSize < WiiSectorSize)
                throw new InvalidDataException("WBFS 섹터 크기가 올바르지 않습니다.");

            if (header[12] == 0)
                throw new InvalidDataException("WBFS 파일에 디스크가 없습니다.");

            long blocksPerDisc = (WiiSectorCount * WiiSectorSize + wbfsSectorSize - 1) / wbfsSectorSize;

            var wlbaTable = new ushort[blocksPerDisc];
            byte[] wlbaBytes = new byte[blocksPerDisc * 2];
            RvzIo.ReadExactly(primaryHandle, wlbaBytes, hdSectorSize + WiiDiscHeaderSize);

            for (long i = 0; i < blocksPerDisc; i++)
                wlbaTable[i] = BinaryPrimitives.ReadUInt16BigEndian(wlbaBytes.AsSpan((int)(i * 2)));

            return new WbfsSource(files, hdSectorSize, wbfsSectorSize, wbfsSectorShift, blocksPerDisc, wlbaTable);
        }
        catch
        {
            for (int i = 1; i < files.Count; i++)
                files[i].Handle.Dispose();

            throw;
        }
    }

    private static string ReplaceLastChar(string path, char replacement)
    {
        return path.Length == 0 ? path : path[..^1] + replacement;
    }

    public void Read(long offset, Span<byte> destination)
    {
        if (offset < 0 || offset + destination.Length > Length)
            throw new EndOfStreamException("WBFS 범위를 벗어난 읽기입니다.");

        int written = 0;
        while (written < destination.Length)
        {
            long currentOffset = offset + written;
            long baseCluster = currentOffset >> _wbfsSectorShift;

            if (baseCluster >= _blocksPerDisc)
                throw new EndOfStreamException("WBFS 디스크 범위를 벗어난 읽기입니다.");

            long clusterAddress = _wbfsSectorSize * _wlbaTable[baseCluster];
            long clusterOffset = currentOffset & (_wbfsSectorSize - 1);
            long finalAddress = clusterAddress + clusterOffset;

            bool found = false;
            foreach (var entry in _files)
            {
                if (finalAddress >= entry.BaseAddress + entry.Size)
                    continue;

                long fileOffset = finalAddress - entry.BaseAddress;
                long tillEndOfFile = entry.Size - fileOffset;
                long tillEndOfSector = _wbfsSectorSize - clusterOffset;
                int chunk = (int)Math.Min(Math.Min(tillEndOfFile, tillEndOfSector), destination.Length - written);

                if (chunk <= 0)
                    throw new EndOfStreamException("WBFS 파일이 예상보다 일찍 끝났습니다.");

                RvzIo.ReadExactly(entry.Handle, destination.Slice(written, chunk), fileOffset);
                written += chunk;
                found = true;
                break;
            }

            if (!found)
                throw new InvalidDataException("WBFS 클러스터 위치가 파일 범위를 벗어났습니다.");
        }
    }

    public void Dispose()
    {
        foreach (var entry in _files)
            entry.Handle.Dispose();
    }
}
