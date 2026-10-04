using System.Security.Cryptography;
using System.Text;
using DolphinTool.Core.Models;

namespace DolphinTool.Core.Services.Wii;

public static class WiiIsoRebuilder
{
    private const int CopyChunkSize = 0x100000;

    public static bool Verify(string inputPath, string reportPath, Action<double>? progress = null, CancellationToken ct = default)
    {
        using var input = RvzInputSource.Open(inputPath);
        var specs = ReadSpecs(input);
        using var writer = new StreamWriter(reportPath, false, new UTF8Encoding(false));
        long total = specs.Sum(s => s.DataSize);
        var reporter = new ProgressReporter(total, progress);
        bool allPassed = true;

        writer.WriteLine($"source: {inputPath}");
        writer.WriteLine($"partitions: {specs.Count}");

        for (int p = 0; p < specs.Count; p++)
        {
            var spec = specs[p];

            writer.WriteLine();
            writer.WriteLine($"[partition {p}] container=0x{spec.ContainerOffset:X} data=0x{spec.DataStart:X} size={spec.DataSize}");

            try
            {
                var result = WiiPartitionVerifier.Verify(input, spec, writer, reporter.Add, ct);
                bool passed = result.Mismatches == 0 && result.H3Matches && result.TmdHashMatches;

                allPassed &= passed;

                writer.WriteLine($"groups: {result.Groups}");
                writer.WriteLine($"groupMismatches: {result.Mismatches}");
                writer.WriteLine($"h3Matches: {result.H3Matches}");
                writer.WriteLine($"tmdHashMatches: {result.TmdHashMatches}");
                writer.WriteLine($"result: {(passed ? "PASS" : "FAIL")}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                allPassed = false;

                writer.WriteLine($"error: {ex.GetType().Name}: {ex.Message}");
            }
        }

        return allPassed;
    }

    public static void RebuildIdentity(string inputPath, string outputPath, Action<double>? progress = null, CancellationToken ct = default)
    {
        bool succeeded = false;

        try
        {
            using var input = RvzInputSource.Open(inputPath);
            var specs = ReadSpecs(input).OrderBy(s => s.DataStart).ToList();
            long length = input.Length;
            using var handle = File.OpenHandle(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, FileOptions.None, length);
            var sink = new FileIsoSink(handle);

            sink.SetLength(length);

            var reporter = new ProgressReporter(length, progress);
            byte[] buffer = new byte[CopyChunkSize];
            long cursor = 0;

            foreach (var spec in specs)
            {
                if (spec.DataStart < cursor)
                    throw new InvalidDataException("Wii 파티션 데이터 영역이 겹칩니다.");

                CopyRaw(input, sink, buffer, cursor, spec.DataStart - cursor, reporter, ct);

                var (h3, used) = EncodePartition(input, sink, spec, reporter, ct);

                PatchHeader(input, sink, spec, h3, used);

                cursor = spec.DataStart + spec.DataSize;
            }

            CopyRaw(input, sink, buffer, cursor, length - cursor, reporter, ct);

            succeeded = true;
        }
        finally
        {
            if (!succeeded)
            {
                try
                {
                    if (File.Exists(outputPath))
                        File.Delete(outputPath);
                }
                catch { }
            }
        }
    }

    private static List<WiiPartitionSpec> ReadSpecs(IRvzInputSource input)
    {
        if (input.Length < 0x20)
            throw new InvalidDataException("Wii 디스크가 아닙니다.");

        byte[] header = new byte[0x20];

        input.Read(0, header);

        if (!RvzWiiWriter.IsWii(header))
            throw new InvalidDataException("Wii 디스크가 아닙니다.");

        var specs = WiiPartitionTable.Read(input, input.Length);

        if (specs.Count == 0)
            throw new InvalidDataException("Wii 파티션을 찾을 수 없습니다.");

        return specs;
    }

    private static void CopyRaw(IRvzInputSource input, IIsoSink sink, byte[] buffer, long offset, long count, ProgressReporter reporter, CancellationToken ct)
    {
        long end = offset + count;

        while (offset < end)
        {
            ct.ThrowIfCancellationRequested();

            int size = (int)Math.Min(buffer.Length, end - offset);
            var span = buffer.AsSpan(0, size);

            input.Read(offset, span);

            if (span.ContainsAnyExcept((byte)0))
                sink.Write(offset, span);

            offset += size;

            reporter.Add(size);
        }
    }

    private static (byte[] H3, int Used) EncodePartition(IRvzInputSource input, IIsoSink sink, WiiPartitionSpec spec, ProgressReporter reporter, CancellationToken ct)
    {
        using var reader = new WiiPartitionReader(input, spec);
        using var encoder = new WiiPartitionEncoder();
        long groups = WiiPartitionEncoder.GetGroupCount(reader);

        if (groups * WiiLayout.HashSize > WiiPartitionHeader.H3TableSize)
            throw new InvalidDataException("파티션이 H3 테이블 용량을 초과합니다.");

        byte[] h3 = new byte[WiiPartitionHeader.H3TableSize];

        for (long group = 0; group < groups; group++)
        {
            ct.ThrowIfCancellationRequested();

            int length = encoder.EncodeGroup(spec.Key, reader, group, h3.AsSpan((int)(group * WiiLayout.HashSize), WiiLayout.HashSize));

            sink.Write(spec.DataStart + group * WiiLayout.GroupTotalSize, encoder.Encrypted[..length]);

            reporter.Add(length);
        }

        return (h3, (int)(groups * WiiLayout.HashSize));
    }

    private static void PatchHeader(IRvzInputSource input, IIsoSink sink, WiiPartitionSpec spec, byte[] h3, int used)
    {
        var header = WiiPartitionHeader.Read(input, spec.ContainerOffset);
        byte[] originalH3 = new byte[WiiPartitionHeader.H3TableSize];

        input.Read(header.H3Offset, originalH3);

        if (h3.AsSpan(0, used).SequenceEqual(originalH3.AsSpan(0, used)))
            return;

        byte[] merged = (byte[])originalH3.Clone();

        h3.AsSpan(0, used).CopyTo(merged);
        sink.Write(header.H3Offset, merged);
        sink.Write(header.TmdOffset + WiiPartitionHeader.TmdContentHashOffset, SHA1.HashData(merged));
    }
}