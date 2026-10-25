using Common;

namespace Patch.Core.Services;

public static class PatchApplier
{
    public static Stream OpenPatched(string path, PatchPlan plan, Func<Stream> openOriginal, long originalLength, Action<string, LogLevel>? log, CancellationToken ct)
    {
        if (plan.TryGetOverwrite(path, out var overwrite))
        {
            log?.Invoke($"교체: {overwrite.DisplayName} → {path}", LogLevel.Info);
            return overwrite.OpenRead();
        }

        if (!plan.IsPatched(path))
            return openOriginal();

        return new MemoryStream(ApplyPatches(path, plan, openOriginal, originalLength, log, ct), writable: false);
    }

    public static byte[] ApplyPatches(string path, PatchPlan plan, Func<Stream> openOriginal, long originalLength, Action<string, LogLevel>? log, CancellationToken ct)
    {
        byte[] original = ReadAll(openOriginal, originalLength);
        return ApplyPatchesAsync(path, plan, original, log, ct).GetAwaiter().GetResult().Data;
    }

    public static Task<(byte[] Data, bool Applied)> ApplyPatchesAsync(string path, PatchPlan plan, byte[] original, Action<string, LogLevel>? log, CancellationToken ct)
        => ApplyPatchesAsync(path, plan.GetPatches(path), original, log, ct);

    public static async Task<(byte[] Data, bool Applied)> ApplyPatchesAsync(string path, IReadOnlyList<PlannedPatch> patches, byte[] original, Action<string, LogLevel>? log, CancellationToken ct)
    {
        foreach (var patch in patches)
        {
            ct.ThrowIfCancellationRequested();

            byte[] patchData = await patch.File.ReadSmallFileBytesAsync(ct);

            if (patch.Strict)
            {
                byte[] patched = await UniversalPatcher.ApplyPatchAsync(original, patchData, null, ct);

                log?.Invoke($"패치 완료: {patch.File.DisplayName} → {path} ({original.Length:N0} → {patched.Length:N0} bytes)", LogLevel.Info);

                return (patched, true);
            }

            try
            {
                byte[] patched = await UniversalPatcher.ApplyPatchAsync(original, patchData, null, ct);

                log?.Invoke($"패치 완료: {patch.File.DisplayName} → {path} ({original.Length:N0} → {patched.Length:N0} bytes)", LogLevel.Info);

                return (patched, true);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                log?.Invoke($"⚠️ 원본이 맞지 않아 건너뜀: {patch.File.DisplayName} → {path} ({ex.Message})", LogLevel.Info);
            }
        }

        return (original, false);
    }

    private static byte[] ReadAll(Func<Stream> openOriginal, long expectedSize)
    {
        using var stream = openOriginal();
        using var ms = new MemoryStream(checked((int)expectedSize));

        stream.CopyTo(ms);

        return ms.Length == ms.Capacity ? ms.GetBuffer() : ms.ToArray();
    }
}