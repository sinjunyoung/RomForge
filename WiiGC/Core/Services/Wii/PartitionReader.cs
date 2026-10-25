using System.Security.Cryptography;
using WiiGC.Core.Interfaces;
using WiiGC.Core.Models.Wii;

namespace WiiGC.Core.Services.Wii;

internal sealed class PartitionReader : IWiiPartitionData, IDisposable
{
    private const int IvSize = 16;

    private readonly IRvzInputSource _input;
    private readonly PartitionSpec _spec;
    private readonly Aes _aes;
    private readonly byte[] _encrypted = new byte[Layout.BlockTotalSize];
    private readonly byte[] _plain = new byte[Layout.BlockDataSize];
    private long _cached = -1;

    public PartitionReader(IRvzInputSource input, PartitionSpec spec)
    {
        _input = input;
        _spec = spec;
        _aes = Aes.Create();
        _aes.Key = spec.Key;
    }

    public long Length => _spec.DataSize / Layout.BlockTotalSize * Layout.BlockDataSize;

    public void Read(long offset, Span<byte> destination) => Read(offset, destination, null);

    public void Read(long offset, Span<byte> destination, bool[]? used)
    {
        if (offset < 0)
            throw new EndOfStreamException("파티션 범위를 벗어난 읽기입니다.");

        int written = 0;

        while (written < destination.Length)
        {
            long cluster = offset / Layout.BlockDataSize;
            int inner = (int)(offset % Layout.BlockDataSize);
            int chunk = Math.Min(Layout.BlockDataSize - inner, destination.Length - written);

            LoadCluster(cluster);

            if (used != null)
            {
                long sector = _spec.DataStart / Layout.BlockTotalSize + cluster;

                if (sector < used.Length)
                    used[sector] = true;
            }

            _plain.AsSpan(inner, chunk).CopyTo(destination.Slice(written, chunk));

            written += chunk;
            offset += chunk;
        }
    }

    private void LoadCluster(long cluster)
    {
        if (_cached == cluster)
            return;

        long relative = cluster * Layout.BlockTotalSize;
        long position = _spec.DataStart + relative;

        if (relative + Layout.BlockTotalSize > _spec.DataSize || position + Layout.BlockTotalSize > _input.Length)
            throw new EndOfStreamException("파티션 범위를 벗어난 읽기입니다.");

        _input.Read(position, _encrypted);
        _aes.DecryptCbc(_encrypted.AsSpan(Layout.BlockHeaderSize, Layout.BlockDataSize), _encrypted.AsSpan(Layout.IvOffset, IvSize), _plain, PaddingMode.None);

        _cached = cluster;
    }

    public void Dispose() => _aes.Dispose();
}