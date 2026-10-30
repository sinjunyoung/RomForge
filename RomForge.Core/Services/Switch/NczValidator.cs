using Common;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.NSZ;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using Path = System.IO.Path;
using FileEntry = (string Name, System.Func<System.IO.Stream, System.Action<long>, System.Threading.Tasks.Task> Writer, long EstimatedSize, string Label);

namespace RomForge.Core.Services.Switch;

internal static class NczValidator
{
    public static async Task ValidateAsync(IFileSystem validationFs, Dictionary<string, NcaToNczConverter> converters, List<FileEntry> fileEntries, string? progressId, Func<string, string> labelOf, string startText, string endText,
        IProgress<ProgressInfo>? progress, Action<string, LogLevel, string?>? log, string? logId, CancellationToken ct)
    {
        var nczEntries = validationFs.EnumerateEntries("/", "*.ncz")
            .Where(e => converters.ContainsKey(Path.ChangeExtension(e.Name, ".nca")))
            .ToList();
        long totalValidationSize = nczEntries.Sum(e => fileEntries.FirstOrDefault(f => string.Equals(f.Name, e.Name, StringComparison.OrdinalIgnoreCase)).EstimatedSize);

        foreach (var entry in nczEntries)
        {
            ct.ThrowIfCancellationRequested();

            string origName = Path.ChangeExtension(entry.Name, ".nca");

            if (!converters.TryGetValue(origName, out var converter))
                continue;

            using var nczFile = new UniqueRef<IFile>();

            validationFs.OpenFile(ref nczFile.Ref, entry.FullPath.ToU8Span(), OpenMode.Read).ThrowIfFailure();

            string label = labelOf(entry.Name);

            log?.Invoke($"- {label} {startText}", LogLevel.Info, logId);
            await converter.ValidateAsync(nczFile.Get.AsStream(), progressId, totalValidationSize, label, progress, ct);
            log?.Invoke($"- {label} {endText}", LogLevel.Ok, logId);
        }
    }
}