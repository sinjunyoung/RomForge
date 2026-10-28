using Common;
using Patch.Core.Enums;
using Patch.Core.Models;
using System.Diagnostics.CodeAnalysis;

namespace Patch.Core.Services;

public sealed class PatchPlan
{
    private readonly Dictionary<string, PatchFileRef> _overwrites;
    private readonly Dictionary<string, PatchFileRef> _unmatchedOverwrites;
    private readonly Dictionary<string, List<PlannedPatch>> _patches;

    private PatchPlan(StringComparer comparer)
    {
        _overwrites = new Dictionary<string, PatchFileRef>(comparer);
        _unmatchedOverwrites = new Dictionary<string, PatchFileRef>(comparer);
        _patches = new Dictionary<string, List<PlannedPatch>>(comparer);
    }

    public int OverwriteCount => _overwrites.Count;

    public int PatchedCount => _patches.Values.Count(list => list.Any(p => p.Strict));

    public int LooseCandidateCount => _patches.Values.Count(list => list.All(p => !p.Strict));

    public bool HasAnyMatch => OverwriteCount > 0 || _patches.Count > 0;

    public IReadOnlyDictionary<string, PatchFileRef> Overwrites => _overwrites;

    public IReadOnlyDictionary<string, PatchFileRef> UnmatchedOverwrites => _unmatchedOverwrites;

    public IEnumerable<string> PatchedPaths => _patches.Keys;

    public bool TryGetOverwrite(string path, [NotNullWhen(true)] out PatchFileRef? file) => _overwrites.TryGetValue(path, out file);

    public bool IsPatched(string path) => _patches.ContainsKey(path);

    public IReadOnlyList<PlannedPatch> GetPatches(string path) => _patches.TryGetValue(path, out var list) ? list : [];

    public static PatchPlan Build(PatchFileIndex index, IEnumerable<string> targetPaths, StringComparer? comparer = null)
    {
        comparer ??= StringComparer.Ordinal;

        var plan = new PatchPlan(comparer);
        var targets = targetPaths.ToList();
        var targetSet = new HashSet<string>(targets, comparer);
        var binaryEntries = new List<(PatchFileEntry Entry, string Key)>();

        foreach (var entry in index.Entries)
        {
            string relDir = entry.RelativeDir.Replace(Path.DirectorySeparatorChar, '/');
            string key = relDir.Length == 0 ? entry.BaseName : $"{relDir}/{entry.BaseName}";

            if (entry.Kind == PatchFileKind.Overwrite)
            {
                if (targetSet.Contains(key))
                    plan._overwrites[key] = entry.File;
                else
                    plan._unmatchedOverwrites[key] = entry.File;

                continue;
            }

            binaryEntries.Add((entry, key));
        }

        var exactKeys = new HashSet<string>(comparer);

        foreach (var (entry, key) in binaryEntries)
        {
            if (!targetSet.Contains(key) || plan._overwrites.ContainsKey(key) || !exactKeys.Add(key))
                continue;

            plan._patches[key] = [new PlannedPatch(entry.File, true)];
        }

        foreach (var (entry, _) in binaryEntries)
        {
            if (entry.BaseName.Length == 0)
                continue;

            var candidates = targets.Where(t => !plan._overwrites.ContainsKey(t) && !exactKeys.Contains(t) && NameMatches(FileNameOf(t), entry.BaseName)).ToList();

            if (candidates.Count == 0 || !PatchMatchPolicy.AllowsLooseNameMatch(DetectFormat(entry.File)))
                continue;

            foreach (string candidate in candidates)
            {
                if (!plan._patches.TryGetValue(candidate, out var list))
                    plan._patches[candidate] = list = [];

                list.Add(new PlannedPatch(entry.File, false));
            }
        }

        return plan;
    }

    public void LogSummary(Action<string, LogLevel>? log, string? label = null)
    {
        if (log is null)
            return;

        string suffix = string.IsNullOrEmpty(label) ? string.Empty : $" ({label})";

        if (!HasAnyMatch)
        {
            log($"패치 대상 파일이 존재하지 않습니다.{suffix}", LogLevel.Error);
            return;
        }

        if (OverwriteCount > 0)
            log($"교체 완료: {OverwriteCount}개 파일{suffix}", LogLevel.Ok);

        if (PatchedCount > 0)
            log($"패치 적용 완료: {PatchedCount}개 파일{suffix}", LogLevel.Ok);

        if (LooseCandidateCount > 0)
            log($"포함 매칭 패치 후보: {LooseCandidateCount}개 파일, 원본이 맞는 파일에만 적용{suffix}", LogLevel.Info);
    }

    private static PatchFormat DetectFormat(PatchFileRef file)
    {
        byte[] header = new byte[8];
        using var stream = file.OpenRead();
        int read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);

        return UniversalPatcher.DetectFormat(header.AsSpan(0, read));
    }

    private static bool NameMatches(string targetFileName, string patchBaseName) => targetFileName.Contains(patchBaseName, StringComparison.OrdinalIgnoreCase) || patchBaseName.Contains(targetFileName, StringComparison.OrdinalIgnoreCase);

    private static string FileNameOf(string path)
    {
        int idx = path.LastIndexOf('/');
        return idx < 0 ? path : path[(idx + 1)..];
    }
}