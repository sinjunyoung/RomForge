using Common;
using Patch.Core.Models;
using Patch.Core.Services;
using System.IO;
using Path = System.IO.Path;

namespace NSW.Core.Services;

public sealed class DiskPatchTarget(string directory) : IPatchTarget
{
    private readonly string _directory = directory;

    public IEnumerable<string> EnumerateRelativeFiles() => Directory.EnumerateFiles(_directory, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(_directory, f).Replace('\\', '/'));

    public void Overwrite(string relativePath, PatchFileRef file) => PatchDiskApplier.CopyTo(file, ResolvePath(relativePath));

    public Task<bool> PatchAsync(string relativePath, PlannedPatch patch, string displayPath, IProgress<ProgressInfo> progress, Action<string, LogLevel> log, CancellationToken ct) => PatchDiskApplier.ApplyAsync(ResolvePath(relativePath), patch, displayPath, progress, log, ct);

    private string ResolvePath(string relativePath) => Path.Combine(_directory, relativePath.Replace('/', Path.DirectorySeparatorChar));
}