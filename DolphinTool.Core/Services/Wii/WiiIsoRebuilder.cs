using DolphinTool.Core.Models;
using Microsoft.Win32.SafeHandles;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace DolphinTool.Core.Services.Wii;

public static class WiiIsoRebuilder
{
    private const int CopyChunkSize = 0x100000;
    private const int BootPointerStart = 0x424;
    private const int BootPointerEnd = 0x430;

    public static IReadOnlyList<string> RebuildWithRiivolution(string inputPath, string outputPath, string xmlOrFolder, IReadOnlyDictionary<string, int>? choices = null, Action<double>? progress = null, CancellationToken ct = default)
    {
        var patch = RiivolutionParser.Parse(xmlOrFolder, choices);

        EnsureGameMatches(inputPath, patch);

        RebuildWithReplacements(inputPath, outputPath, patch.Replacements, progress, ct);

        return patch.Warnings;
    }

    public static bool VerifyRiivolution(string originalPath, string xmlOrFolder, string rebuiltPath, string reportPath, IReadOnlyDictionary<string, int>? choices = null, CancellationToken ct = default)
    {
        var patch = RiivolutionParser.Parse(xmlOrFolder, choices);

        return VerifyAgainstOriginal(originalPath, patch.Replacements, rebuiltPath, reportPath, ct);
    }

    private static void EnsureGameMatches(string inputPath, RiivolutionPatchSet patch)
    {
        using var input = RvzInputSource.Open(inputPath);

        if (input.Length < 6)
            throw new InvalidDataException("Wii 디스크가 아닙니다.");

        byte[] id = new byte[6];

        input.Read(0, id);

        string discId = Encoding.ASCII.GetString(id);

        if (!patch.MatchesDisc(discId))
            throw new InvalidDataException($"패치 대상 게임({string.Join(", ", patch.GameIds)})과 디스크({discId})가 다릅니다.");
    }

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

    public static bool VerifyFolder(string isoPath, string folder, string reportPath, CancellationToken ct = default)
    {
        using var input = RvzInputSource.Open(isoPath);
        var specs = ReadSpecs(input);
        var replacements = CollectFolder(folder);
        using var writer = new StreamWriter(reportPath, false, new UTF8Encoding(false));

        writer.WriteLine($"source: {isoPath}");
        writer.WriteLine($"folder: {folder}");
        writer.WriteLine($"files: {replacements.Count}");

        var target = FindTargets(input, specs).FirstOrDefault() ?? throw new InvalidDataException("게임 파티션을 찾을 수 없습니다.");
        using var reader = new WiiPartitionReader(input, target);
        var info = WiiFileSystem.Read(reader);
        var byPath = new Dictionary<string, WiiFileEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in info.Files)
            byPath.TryAdd(file.Path, file);

        int ok = 0;
        int bad = 0;
        byte[] discBuffer = new byte[CopyChunkSize];
        byte[] fileBuffer = new byte[CopyChunkSize];

        foreach (var (discPath, filePath) in replacements.OrderBy(r => r.Key, StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();

            if (!byPath.TryGetValue(discPath, out var entry))
            {
                bad++;

                writer.WriteLine($"MISSING {discPath}");

                continue;
            }

            long fileSize = new FileInfo(filePath).Length;

            if (fileSize != entry.Size)
            {
                bad++;

                writer.WriteLine($"SIZE    {discPath} disc={entry.Size} file={fileSize}");

                continue;
            }

            bool same = true;
            using var handle = File.OpenHandle(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan);

            for (long position = 0; position < fileSize && same; position += CopyChunkSize)
            {
                int size = (int)Math.Min(CopyChunkSize, fileSize - position);

                reader.Read(entry.Offset + position, discBuffer.AsSpan(0, size));
                RvzIo.ReadExactly(handle, fileBuffer.AsSpan(0, size), position);

                same = discBuffer.AsSpan(0, size).SequenceEqual(fileBuffer.AsSpan(0, size));
            }

            if (same)
                ok++;
            else
            {
                bad++;

                writer.WriteLine($"DIFF    {discPath}");
            }
        }

        writer.WriteLine($"ok: {ok}");
        writer.WriteLine($"bad: {bad}");
        writer.WriteLine($"result: {(bad == 0 ? "PASS" : "FAIL")}");

        return bad == 0;
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

                using var reader = new WiiPartitionReader(input, spec);

                byte[] h3 = EncodePartition(reader, spec.Key, spec.DataStart, sink, reporter, ct);

                PatchHeader(input, sink, spec, h3, null);

                cursor = spec.DataStart + spec.DataSize;
            }

            CopyRaw(input, sink, buffer, cursor, length - cursor, reporter, ct);

            succeeded = true;
        }
        finally
        {
            DeleteIfFailed(outputPath, succeeded);
        }
    }

    public static void RebuildWithFolder(string inputPath, string outputPath, string folder, Action<double>? progress = null, CancellationToken ct = default) => RebuildWithReplacements(inputPath, outputPath, CollectFolder(folder), progress, ct);

    public static void RebuildWithReplacements(string inputPath, string outputPath, IReadOnlyDictionary<string, string> replacements, Action<double>? progress = null, CancellationToken ct = default)
    {
        if (replacements.Count == 0)
            throw new InvalidDataException("교체할 파일이 없습니다.");

        bool succeeded = false;
        var readers = new List<WiiPartitionReader>();
        var plans = new List<(WiiPartitionSpec Spec, WiiRepackPlan Plan)>();

        try
        {
            using var input = RvzInputSource.Open(inputPath);
            var specs = ReadSpecs(input);
            long length = input.Length;

            foreach (var spec in FindTargets(input, specs))
            {
                var reader = new WiiPartitionReader(input, spec);

                readers.Add(reader);

                var plan = WiiPartitionPlanner.Plan(reader, replacements);

                if (plan.DataSize > spec.DataSize)
                    throw new InvalidDataException("교체 후 파티션 크기가 원본 파티션 영역을 초과합니다.");

                plans.Add((spec, plan));
            }

            if (plans.Count == 0)
                throw new InvalidDataException("게임 파티션을 찾을 수 없습니다.");

            plans.Sort((a, b) => a.Spec.DataStart.CompareTo(b.Spec.DataStart));

            using var handle = File.OpenHandle(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, FileOptions.None, length);
            var sink = new FileIsoSink(handle);

            sink.SetLength(length);

            long rawBytes = length - plans.Sum(p => p.Spec.DataSize);
            var reporter = new ProgressReporter(rawBytes + plans.Sum(p => p.Plan.DataSize), progress);
            byte[] buffer = new byte[CopyChunkSize];
            long cursor = 0;

            foreach (var (spec, plan) in plans)
            {
                if (spec.DataStart < cursor)
                    throw new InvalidDataException("Wii 파티션 데이터 영역이 겹칩니다.");

                CopyRaw(input, sink, buffer, cursor, spec.DataStart - cursor, reporter, ct);

                byte[] h3 = EncodePartition(plan.Data, spec.Key, spec.DataStart, sink, reporter, ct);

                PatchHeader(input, sink, spec, h3, plan.DataSize);

                cursor = spec.DataStart + spec.DataSize;
            }

            CopyRaw(input, sink, buffer, cursor, length - cursor, reporter, ct);

            succeeded = true;
        }
        finally
        {
            foreach (var (_, plan) in plans)
                plan.Data.Dispose();

            foreach (var reader in readers)
                reader.Dispose();

            DeleteIfFailed(outputPath, succeeded);
        }
    }

    private static Dictionary<string, string> CollectFolder(string folder)
    {
        string root = Path.GetFullPath(folder);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            result["/" + Path.GetRelativePath(root, file).Replace('\\', '/')] = file;

        if (result.Count == 0)
            throw new InvalidDataException("교체 폴더에 파일이 없습니다.");

        return result;
    }

    private static List<WiiPartitionSpec> FindTargets(IRvzInputSource input, List<WiiPartitionSpec> specs)
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

    private static byte[] EncodePartition(IWiiPartitionData data, byte[] key, long dataStart, IIsoSink sink, ProgressReporter reporter, CancellationToken ct)
    {
        using var encoder = new WiiPartitionEncoder();

        long groups = WiiPartitionEncoder.GetGroupCount(data);

        if (groups * WiiLayout.HashSize > WiiPartitionHeader.H3TableSize)
            throw new InvalidDataException("파티션이 H3 테이블 용량을 초과합니다.");

        byte[] h3 = new byte[WiiPartitionHeader.H3TableSize];

        for (long group = 0; group < groups; group++)
        {
            ct.ThrowIfCancellationRequested();

            int length = encoder.EncodeGroup(key, data, group, h3.AsSpan((int)(group * WiiLayout.HashSize), WiiLayout.HashSize));

            sink.Write(dataStart + group * WiiLayout.GroupTotalSize, encoder.Encrypted[..length]);

            reporter.Add(length);
        }

        return h3;
    }

    private static void PatchHeader(IRvzInputSource input, IIsoSink sink, WiiPartitionSpec spec, byte[] h3, long? dataSize)
    {
        var header = WiiPartitionHeader.Read(input, spec.ContainerOffset);
        byte[] originalH3 = new byte[WiiPartitionHeader.H3TableSize];

        input.Read(header.H3Offset, originalH3);

        if (dataSize.HasValue)
        {
            byte[] size = new byte[4];

            BinaryPrimitives.WriteUInt32BigEndian(size, (uint)(dataSize.Value >> 2));

            sink.Write(spec.ContainerOffset + 0x2BC, size);
        }

        if (h3.AsSpan().SequenceEqual(originalH3))
            return;

        sink.Write(header.H3Offset, h3);

        byte[] tmd = new byte[header.TmdSize];

        input.Read(header.TmdOffset, tmd);
        WiiTmd.SetContentHash(tmd, SHA1.HashData(h3));
        WiiTmd.FakeSign(tmd);
        sink.Write(header.TmdOffset, tmd);
    }

    private static void DeleteIfFailed(string outputPath, bool succeeded)
    {
        if (succeeded)
            return;

        try
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
        }
        catch { }
    }



    public static bool VerifyAgainstOriginal(string originalPath, string folder, string rebuiltPath, string reportPath, CancellationToken ct = default) => VerifyAgainstOriginal(originalPath, CollectFolder(folder), rebuiltPath, reportPath, ct);

    public static bool VerifyAgainstOriginal(string originalPath, IReadOnlyDictionary<string, string> overlay, string rebuiltPath, string reportPath, CancellationToken ct = default)
    {
        using var originalInput = RvzInputSource.Open(originalPath);
        using var rebuiltInput = RvzInputSource.Open(rebuiltPath);
        var originalTarget = FindTargets(originalInput, ReadSpecs(originalInput)).FirstOrDefault() ?? throw new InvalidDataException("원본의 게임 파티션을 찾을 수 없습니다.");
        var rebuiltTarget = FindTargets(rebuiltInput, ReadSpecs(rebuiltInput)).FirstOrDefault() ?? throw new InvalidDataException("결과물의 게임 파티션을 찾을 수 없습니다.");
        using var original = new WiiPartitionReader(originalInput, originalTarget);
        using var rebuilt = new WiiPartitionReader(rebuiltInput, rebuiltTarget);
        var originalFiles = WiiFileSystem.Read(original).Files;
        var rebuiltFiles = WiiFileSystem.Read(rebuilt).Files;
        using var writer = new StreamWriter(reportPath, false, new UTF8Encoding(false));

        writer.WriteLine($"original: {originalPath}");
        writer.WriteLine($"rebuilt: {rebuiltPath}");
        writer.WriteLine($"overlay: {overlay.Count} files");

        var rebuiltByPath = new Dictionary<string, WiiFileEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in rebuiltFiles)
            rebuiltByPath.TryAdd(file.Path, file);

        var originalPaths = new HashSet<string>(originalFiles.Select(f => f.Path), StringComparer.OrdinalIgnoreCase);
        int unchanged = 0;
        int replaced = 0;
        int bad = 0;
        byte[] first = new byte[CopyChunkSize];
        byte[] second = new byte[CopyChunkSize];

        foreach (var file in originalFiles.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase))
        {
            ct.ThrowIfCancellationRequested();

            if (!rebuiltByPath.TryGetValue(file.Path, out var result))
            {
                bad++;

                writer.WriteLine($"MISSING   {file.Path}");

                continue;
            }

            overlay.TryGetValue(file.Path, out string? overlayPath);

            long expectedSize = overlayPath != null ? new FileInfo(overlayPath).Length : file.Size;

            if (result.Size != expectedSize)
            {
                bad++;

                writer.WriteLine($"SIZE      {file.Path} expected={expectedSize} rebuilt={result.Size}");

                continue;
            }

            if (SameContent(rebuilt, result.Offset, original, file.Offset, overlayPath, expectedSize, first, second))
            {
                if (overlayPath != null)
                    replaced++;
                else
                    unchanged++;
            }
            else
            {
                bad++;

                writer.WriteLine($"DIFF      {file.Path} ({(overlayPath != null ? "overlay" : "original")})");
            }
        }

        foreach (var file in rebuiltFiles)
        {
            if (!originalPaths.Contains(file.Path))
            {
                bad++;

                writer.WriteLine($"EXTRA     {file.Path}");
            }
        }

        foreach (string path in overlay.Keys)
        {
            if (!originalPaths.Contains(path))
            {
                bad++;

                writer.WriteLine($"NOT_ON_DISC {path}");
            }
        }

        bool systemOk = CompareSystemArea(original, rebuilt, writer, first, second);

        writer.WriteLine($"files in original: {originalFiles.Count}");
        writer.WriteLine($"unchanged ok: {unchanged}");
        writer.WriteLine($"replaced ok: {replaced}");
        writer.WriteLine($"bad: {bad}");
        writer.WriteLine($"boot/apploader/dol: {(systemOk ? "OK" : "DIFF")}");

        bool passed = bad == 0 && systemOk;

        writer.WriteLine($"result: {(passed ? "PASS" : "FAIL")}");

        return passed;
    }

    private static bool CompareSystemArea(WiiPartitionReader original, WiiPartitionReader rebuilt, TextWriter writer, byte[] first, byte[] second)
    {
        byte[] originalBoot = new byte[0x440];
        byte[] rebuiltBoot = new byte[0x440];

        original.Read(0, originalBoot);
        rebuilt.Read(0, rebuiltBoot);

        long originalDol = (long)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(originalBoot.AsSpan(0x420)) << 2;
        long rebuiltDol = (long)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(rebuiltBoot.AsSpan(0x420)) << 2;

        if (originalDol != rebuiltDol)
        {
            writer.WriteLine($"SYSTEM    dol offset differs original=0x{originalDol:X} rebuilt=0x{rebuiltDol:X}");

            return false;
        }

        byte[] dolHeader = new byte[WiiDol.HeaderSize];

        original.Read(originalDol, dolHeader);

        long dolSize = WiiDol.GetSize(dolHeader);
        bool ok = true;

        if (!SameContent(rebuilt, 0, original, 0, null, BootPointerStart, first, second))
        {
            writer.WriteLine("SYSTEM    boot header (0x0-0x424) differs");

            ok = false;
        }

        if (!SameContent(rebuilt, BootPointerEnd, original, BootPointerEnd, null, originalDol - BootPointerEnd, first, second))
        {
            writer.WriteLine($"SYSTEM    bi2/apploader (0x{BootPointerEnd:X}-0x{originalDol:X}) differs");

            ok = false;
        }

        if (!SameContent(rebuilt, originalDol, original, originalDol, null, dolSize, first, second))
        {
            writer.WriteLine("SYSTEM    main.dol differs");

            ok = false;
        }

        return ok;
    }

    private static bool SameContent(WiiPartitionReader rebuilt, long rebuiltOffset, WiiPartitionReader original, long originalOffset, string? filePath, long size, byte[] first, byte[] second)
    {
        SafeFileHandle? handle = filePath == null ? null : File.OpenHandle(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan);

        try
        {
            for (long position = 0; position < size; position += first.Length)
            {
                int length = (int)Math.Min(first.Length, size - position);

                rebuilt.Read(rebuiltOffset + position, first.AsSpan(0, length));

                if (handle != null)
                    RvzIo.ReadExactly(handle, second.AsSpan(0, length), position);
                else
                    original.Read(originalOffset + position, second.AsSpan(0, length));

                if (!first.AsSpan(0, length).SequenceEqual(second.AsSpan(0, length)))
                    return false;
            }

            return true;
        }
        finally
        {
            handle?.Dispose();
        }
    }
}