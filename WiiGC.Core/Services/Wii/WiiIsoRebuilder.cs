using System.Buffers.Binary;
using System.Security.Cryptography;
using WiiGC.Core.Models;

namespace WiiGC.Core.Services.Wii;

public static class WiiIsoRebuilder
{
    private const int CopyChunkSize = 0x100000;

    public static WiiPatchResult RebuildWithReplacements(string inputPath, string outputPath, IReadOnlyDictionary<string, string> replacements, Action<double>? progress = null, Action<WiiPatchEntry>? entryLog = null, CancellationToken ct = default)
    {
        bool succeeded = false;
        WiiPatchResult? result = null;
        var readers = new List<WiiPartitionReader>();
        var targets = new List<WiiRebuildTarget>();

        try
        {
            using var input = RvzInputSource.Open(inputPath);
            long length = input.Length;

            PlanTargets(input, replacements, readers, targets);

            result = new WiiPatchResult(targets[0].Plan.Entries);

            using var handle = SparseFile.Create(outputPath, length);
            var sink = new FileIsoSink(handle);

            SparseFile.TryMark(handle);
            sink.SetLength(length);

            using var encoder = new WiiPartitionEncoder();
            long rawBytes = length - targets.Sum(t => t.Spec.DataSize);
            var reporter = new ProgressReporter(rawBytes + targets.Sum(t => t.Plan.DataSize), progress);
            byte[] buffer = new byte[CopyChunkSize];
            long cursor = 0;

            foreach (var target in targets)
            {
                SparseFile.CopyNonZero(input, sink, buffer, cursor, target.Spec.DataStart - cursor, reporter, ct);

                byte[] h3 = EncodePartition(encoder, target, sink, entryLog, reporter.Add, ct);

                PatchHeader(input, sink, target.Spec, h3, target.Plan.DataSize);

                cursor = target.Spec.DataStart + target.Spec.DataSize;
            }

            SparseFile.CopyNonZero(input, sink, buffer, cursor, length - cursor, reporter, ct);

            succeeded = true;
        }
        finally
        {
            foreach (var target in targets)
                target.Plan.Data.Dispose();

            foreach (var reader in readers)
                reader.Dispose();

            OutputGuard.DeleteIfFailed(outputPath, succeeded);
        }

        return result!;
    }

    internal static void PlanTargets(IRvzInputSource input, IReadOnlyDictionary<string, string> replacements, List<WiiPartitionReader> readers, List<WiiRebuildTarget> targets)
    {
        long cursor = 0;

        foreach (var spec in FindTargets(input, ReadSpecs(input)).OrderBy(s => s.DataStart))
        {
            if (spec.DataStart < cursor)
                throw new InvalidDataException("Wii 파티션 데이터 영역이 겹칩니다.");

            var reader = new WiiPartitionReader(input, spec);

            readers.Add(reader);

            var plan = WiiPartitionPlanner.Plan(reader, replacements);

            if (plan.DataSize > spec.DataSize)
            {
                plan.Data.Dispose();

                throw new InvalidDataException("교체 후 파티션 크기가 원본 파티션 영역을 초과합니다.");
            }

            targets.Add(new WiiRebuildTarget(targets.Count, spec, plan));

            cursor = spec.DataStart + spec.DataSize;
        }

        if (targets.Count == 0)
            throw new InvalidDataException("게임 파티션을 찾을 수 없습니다.");
    }

    internal static byte[] EncodePartition(WiiPartitionEncoder encoder, WiiRebuildTarget target, IIsoSink? sink, Action<WiiPatchEntry>? entryLog, Action<long>? onGroup, CancellationToken ct)
    {
        var data = target.Plan.Data;
        long groups = WiiPartitionEncoder.GetGroupCount(data);

        if (groups * WiiLayout.HashSize > WiiPartitionHeader.H3TableSize)
            throw new InvalidDataException("파티션이 H3 테이블 용량을 초과합니다.");

        byte[] h3 = new byte[WiiPartitionHeader.H3TableSize];
        var logger = target.Index == 0 ? new WiiPatchLogger(target.Plan.Entries, entryLog) : null;

        for (long group = 0; group < groups; group++)
        {
            ct.ThrowIfCancellationRequested();
            logger?.Advance((group + 1) * WiiLayout.GroupDataSize);

            int length = encoder.EncodeGroup(target.Spec.Key, data, group, h3.AsSpan((int)(group * WiiLayout.HashSize), WiiLayout.HashSize));

            sink?.Write(target.Spec.DataStart + group * WiiLayout.GroupTotalSize, encoder.Encrypted[..length]);
            onGroup?.Invoke(length);
        }

        logger?.Complete();

        return h3;
    }

    internal static List<WiiPartitionSpec> FindTargets(IRvzInputSource input, List<WiiPartitionSpec> specs)
    {
        byte[] discId = new byte[6];

        input.Read(0, discId);

        var targets = new List<WiiPartitionSpec>();

        foreach (var spec in specs)
        {
            using var reader = new WiiPartitionReader(input, spec);
            byte[] id = new byte[6];

            reader.Read(0, id);

            if (id.AsSpan().SequenceEqual(discId))
                targets.Add(spec);
        }

        return targets;
    }

    internal static List<WiiPartitionSpec> ReadSpecs(IRvzInputSource input)
    {
        if (input.Length < DiscHeader.Size)
            throw new InvalidDataException("Wii 디스크가 아닙니다.");

        byte[] header = new byte[DiscHeader.Size];

        input.Read(0, header);

        if (!DiscHeader.IsWii(header))
            throw new InvalidDataException("Wii 디스크가 아닙니다.");

        var specs = WiiPartitionTable.Read(input, input.Length);

        if (specs.Count == 0)
            throw new InvalidDataException("Wii 파티션을 찾을 수 없습니다.");

        return specs;
    }

    private static void PatchHeader(IRvzInputSource input, FileIsoSink sink, WiiPartitionSpec spec, byte[] h3, long? dataSize)
    {
        foreach (var (offset, data) in BuildHeaderPatches(input, spec, h3, dataSize))
            sink.Write(offset, data);
    }

    internal static List<(long Offset, byte[] Data)> BuildHeaderPatches(IRvzInputSource input, WiiPartitionSpec spec, byte[] h3, long? dataSize)
    {
        var patches = new List<(long Offset, byte[] Data)>();
        var header = WiiPartitionHeader.Read(input, spec.ContainerOffset);
        byte[] originalH3 = new byte[WiiPartitionHeader.H3TableSize];

        input.Read(header.H3Offset, originalH3);

        if (dataSize.HasValue)
        {
            byte[] size = new byte[4];

            BinaryPrimitives.WriteUInt32BigEndian(size, (uint)(dataSize.Value >> 2));

            patches.Add((spec.ContainerOffset + WiiPartitionHeader.DataSizeField, size));
        }

        if (h3.AsSpan().SequenceEqual(originalH3))
            return patches;

        patches.Add((header.H3Offset, h3));

        byte[] tmd = new byte[header.TmdSize];

        input.Read(header.TmdOffset, tmd);
        WiiTmd.SetContentHash(tmd, SHA1.HashData(h3));
        WiiTmd.FakeSign(tmd);
        patches.Add((header.TmdOffset, tmd));

        return patches;
    }
}