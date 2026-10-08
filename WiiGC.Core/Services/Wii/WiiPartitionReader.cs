using System.Security.Cryptography;
using WiiGC.Core.Models;
using WiiGC.Core.Services;

namespace WiiGC.Core.Services.Wii;

internal sealed class WiiPartitionReader : IWiiPartitionData, IDisposable
{
    private const int IvOffset = 0x3D0;
    private const int IvSize = 16;

    private readonly IRvzInputSource _input;
    private readonly WiiPartitionSpec _spec;
    private readonly Aes _aes;
    private readonly byte[] _encrypted = new byte[WiiLayout.BlockTotalSize];
    private readonly byte[] _plain = new byte[WiiLayout.BlockDataSize];
    private long _cached = -1;

    public WiiPartitionReader(IRvzInputSource input, WiiPartitionSpec spec)
    {
        _input = input;
        _spec = spec;
        _aes = Aes.Create();
        _aes.Key = spec.Key;
    }

    public long Length => _spec.DataSize / WiiLayout.BlockTotalSize * WiiLayout.BlockDataSize;

    public void Read(long offset, Span<byte> destination) => Read(offset, destination, null);

    public void Read(long offset, Span<byte> destination, bool[]? used)
    {
        if (offset < 0)
            throw new EndOfStreamException("파티션 범위를 벗어난 읽기입니다.");

        int written = 0;

        while (written < destination.Length)
        {
            long cluster = offset / WiiLayout.BlockDataSize;
            int inner = (int)(offset % WiiLayout.BlockDataSize);
            int chunk = Math.Min(WiiLayout.BlockDataSize - inner, destination.Length - written);

            LoadCluster(cluster);

            if (used != null)
            {
                long sector = _spec.DataStart / WiiLayout.BlockTotalSize + cluster;

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

        long relative = cluster * WiiLayout.BlockTotalSize;
        long position = _spec.DataStart + relative;

        if (relative + WiiLayout.BlockTotalSize > _spec.DataSize || position + WiiLayout.BlockTotalSize > _input.Length)
            throw new EndOfStreamException("파티션 범위를 벗어난 읽기입니다.");

        _input.Read(position, _encrypted);

        _aes.DecryptCbc(_encrypted.AsSpan(WiiLayout.BlockHeaderSize, WiiLayout.BlockDataSize), _encrypted.AsSpan(IvOffset, IvSize), _plain, PaddingMode.None);

        _cached = cluster;
    }

    public void Dispose() => _aes.Dispose();
}