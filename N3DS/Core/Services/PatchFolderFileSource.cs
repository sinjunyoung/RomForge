using Common.Models;
using Common.Localization;
using N3DS.Core.Interfaces;
using Patch.Core.Services;

namespace N3DS.Core.Services;

public class PatchFolderFileSource(PatchFileIndex index, IEnumerable<string> targetPaths, bool cacheOverwrites = false) : IRomFsFileSource
{
    private readonly PatchPlan _plan = PatchPlan.Build(index, targetPaths.Select(NormalizePath), StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _resultCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _overwriteCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _skippedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _appliedPaths = new(StringComparer.OrdinalIgnoreCase);

    public int AppliedCount => _appliedPaths.Count;

    public async ValueTask<Stream?> OpenFileAsync(string fullPath, Func<CancellationToken, ValueTask<Stream?>>? getOriginal = null, Action<string, LogLevel>? log = null, CancellationToken ct = default)
    {
        string relative = NormalizePath(fullPath);

        if (_plan.TryGetOverwrite(relative, out var overwrite))
        {
            if (_appliedPaths.Add(relative))
                log?.Invoke(Loc.S.LogRomfsFileReplaced(relative), LogLevel.Info);

            return OpenOverwrite(relative, overwrite);
        }

        if (_resultCache.TryGetValue(relative, out byte[]? cachedResult))
            return new MemoryStream(cachedResult);

        if (!_plan.IsPatched(relative) || _skippedPaths.Contains(relative))
            return null;

        if (getOriginal == null)
            throw new InvalidOperationException(Loc.S.ErrRomfsPatchNoOriginalReader(relative));

        var originalStream = await getOriginal(ct) ?? throw new FileNotFoundException(Loc.S.ErrRomfsOriginalNotFound(relative));
        byte[] originalData;

        await using (originalStream)
        {
            using var ms = new MemoryStream();

            await originalStream.CopyToAsync(ms, ct);

            originalData = ms.ToArray();
        }

        var (data, applied) = await PatchApplier.ApplyPatchesAsync(relative, _plan, originalData, log, ct);

        if (!applied)
        {
            _skippedPaths.Add(relative);
            return null;
        }

        _appliedPaths.Add(relative);
        _resultCache[relative] = data;

        return new MemoryStream(data);
    }

    private Stream OpenOverwrite(string relative, PatchFileRef overwrite)
    {
        if (!cacheOverwrites || overwrite.IsDisk)
            return overwrite.OpenRead();

        if (!_overwriteCache.TryGetValue(relative, out byte[]? data))
        {
            data = overwrite.ReadSmallFileBytes();
            _overwriteCache[relative] = data;
        }

        return new MemoryStream(data);
    }

    private static string NormalizePath(string path) => path.TrimStart('/').Replace('\\', '/');
}