using System.Security.Cryptography;
using WiiGC.Core.Models;
using WiiGC.Core.Services;

namespace WiiGC.Core.Services.Wii;

internal static class WiiPartitionVerifier
{
    private const int MaxSamples = 10;

    public static WiiVerifyResult Verify(IRvzInputSource input, WiiPartitionSpec spec, TextWriter log, Action<long>? onGroup, CancellationToken ct)
    {
        using var reader = new WiiPartitionReader(input, spec);
        using var encoder = new WiiPartitionEncoder();

        long groups = WiiPartitionEncoder.GetGroupCount(reader);

        if (groups * WiiLayout.HashSize > WiiPartitionHeader.H3TableSize)
            throw new InvalidDataException("파티션이 H3 테이블 용량을 초과합니다.");

        byte[] h3 = new byte[WiiPartitionHeader.H3TableSize];
        byte[] original = new byte[WiiLayout.GroupTotalSize];
        long mismatches = 0;

        for (long group = 0; group < groups; group++)
        {
            ct.ThrowIfCancellationRequested();

            int length = encoder.EncodeGroup(spec.Key, reader, group, h3.AsSpan((int)(group * WiiLayout.HashSize), WiiLayout.HashSize));

            input.Read(spec.DataStart + group * WiiLayout.GroupTotalSize, original.AsSpan(0, length));

            var expected = encoder.Encrypted[..length];

            if (!expected.SequenceEqual(original.AsSpan(0, length)))
            {
                mismatches++;

                if (mismatches <= MaxSamples)
                {
                    int index = expected.CommonPrefixLength(original.AsSpan(0, length));
                    int block = index / WiiLayout.BlockTotalSize;
                    int inBlock = index % WiiLayout.BlockTotalSize;
                    string region = inBlock < WiiLayout.BlockHeaderSize ? "hash" : "data";

                    log.WriteLine($"  mismatch group={group} block={block} offsetInBlock=0x{inBlock:X} region={region}");
                }
            }

            onGroup?.Invoke(length);
        }

        var header = WiiPartitionHeader.Read(input, spec.ContainerOffset);
        byte[] originalH3 = new byte[WiiPartitionHeader.H3TableSize];

        input.Read(header.H3Offset, originalH3);

        bool h3Matches = h3.AsSpan(0, (int)(groups * WiiLayout.HashSize)).SequenceEqual(originalH3.AsSpan(0, (int)(groups * WiiLayout.HashSize)));
        byte[] tmdHash = new byte[WiiLayout.HashSize];

        input.Read(header.TmdOffset + WiiPartitionHeader.TmdContentHashOffset, tmdHash);

        bool tmdMatches = SHA1.HashData(h3).AsSpan().SequenceEqual(tmdHash);

        if (!tmdMatches)
        {
            bool onOriginal = SHA1.HashData(originalH3).AsSpan().SequenceEqual(tmdHash);

            log.WriteLine($"  tmd hash differs from SHA1(computed H3 padded to 0x18000); SHA1(original H3 table)==tmd: {onOriginal}");
        }

        return new WiiVerifyResult(groups, mismatches, h3Matches, tmdMatches);
    }
}