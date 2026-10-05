using Microsoft.Win32.SafeHandles;

namespace WiiGC.Core.Services.Wii;

internal sealed class WiiRepackedPartition : IWiiPartitionData, IDisposable
{
    private readonly List<Extent> _extents;
    private SafeFileHandle? _handle;
    private string? _handlePath;

    public WiiRepackedPartition(List<Extent> extents, long length)
    {
        _extents = extents.OrderBy(e => e.Start).ToList();
        Length = length;

        long end = 0;

        foreach (var extent in _extents)
        {
            if (extent.Start < end || extent.Start + extent.Length > length)
                throw new InvalidDataException("Wii 파티션 배치가 올바르지 않습니다.");

            end = extent.Start + extent.Length;
        }
    }

    public long Length { get; }

    public void Read(long offset, Span<byte> destination)
    {
        if (offset < 0 || offset + destination.Length > Length)
            throw new EndOfStreamException("파티션 범위를 벗어난 읽기입니다.");

        destination.Clear();

        long end = offset + destination.Length;

        for (int i = FindFirst(offset); i < _extents.Count && _extents[i].Start < end; i++)
        {
            var extent = _extents[i];
            long from = Math.Max(offset, extent.Start);
            long to = Math.Min(end, extent.Start + extent.Length);

            if (to <= from)
                continue;

            ReadExtent(extent, from - extent.Start, destination.Slice((int)(from - offset), (int)(to - from)));
        }
    }

    public void Dispose() => _handle?.Dispose();

    private int FindFirst(long offset)
    {
        int low = 0;
        int high = _extents.Count;

        while (low < high)
        {
            int mid = (low + high) / 2;

            if (_extents[mid].Start + _extents[mid].Length > offset)
                high = mid;
            else
                low = mid + 1;
        }

        return low;
    }

    private void ReadExtent(Extent extent, long within, Span<byte> target)
    {
        if (extent.Memory != null)
            extent.Memory.AsSpan((int)within, target.Length).CopyTo(target);
        else if (extent.Reader != null)
            extent.Reader.Read(extent.ReaderOffset + within, target);
        else
        {
            if (_handle == null || _handlePath != extent.FilePath)
            {
                _handle?.Dispose();
                _handle = File.OpenHandle(extent.FilePath!, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.RandomAccess);
                _handlePath = extent.FilePath;
            }

            RvzIo.ReadExactly(_handle, target, within);
        }
    }

    internal sealed record Extent(long Start, long Length, byte[]? Memory, WiiPartitionReader? Reader, long ReaderOffset, string? FilePath)
    {
        public static Extent FromMemory(long start, byte[] memory) => new(start, memory.Length, memory, null, 0, null);

        public static Extent FromReader(long start, long length, WiiPartitionReader reader, long readerOffset) => new(start, length, null, reader, readerOffset, null);

        public static Extent FromFile(long start, long length, string path) => new(start, length, null, null, 0, path);
    }
}