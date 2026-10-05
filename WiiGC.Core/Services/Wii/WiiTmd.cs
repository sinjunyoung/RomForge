using System.Buffers.Binary;
using System.Security.Cryptography;

namespace WiiGC.Core.Services.Wii;

internal static class WiiTmd
{
    private const int SignatureOffset = 4;
    private const int SignatureSize = 0x100;
    private const int SignedStart = 0x140;
    private const int CounterOffset = 0x1D4;
    private const uint MaxAttempts = 0x1000000;

    public static void SetContentHash(byte[] tmd, ReadOnlySpan<byte> hash) => hash.CopyTo(tmd.AsSpan(WiiPartitionHeader.TmdContentHashOffset));

    public static void FakeSign(byte[] tmd)
    {
        tmd.AsSpan(SignatureOffset, SignatureSize).Clear();

        for (uint counter = 0; counter < MaxAttempts; counter++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(tmd.AsSpan(CounterOffset), counter);

            if (SHA1.HashData(tmd.AsSpan(SignedStart))[0] == 0)
                return;
        }

        throw new InvalidOperationException("TMD 가짜 서명에 실패했습니다.");
    }
}