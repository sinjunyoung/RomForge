using Common;
using Patch.Core.Models;
using Patch.Core.Services;

namespace NSW.Core.Services;

public interface IPatchTarget
{
    IEnumerable<string> EnumerateRelativeFiles();

    void Overwrite(string relativePath, PatchFileRef file);

    Task<bool> PatchAsync(string relativePath, PlannedPatch patch, string displayPath, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, CancellationToken ct);
}