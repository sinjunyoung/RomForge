using Common;
using LibHac.Common.Keys;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using System.IO;
using FileEntry = (string Name, System.Func<System.IO.Stream, System.Action<long>, System.Threading.Tasks.Task> Writer, long EstimatedSize, string Label);

namespace RomForge.Core.Services.Switch;

internal sealed class NspOutput : OutputContainer
{
    protected override bool IsXci => false;

    public override Task WriteAsync(SourceSet source, string displayName, string progressId, uint pfsAlignment, List<FileEntry> entries, Stream fout, IProgress<ProgressInfo> progress, List<IDisposable> disposables, CancellationToken ct)
        => Pfs0Builder.WriteAsync(displayName, progressId, entries, fout, pfsAlignment, false, null, progress, ct);

    public override IFileSystem OpenForValidation(Stream fout, KeySet keySet)
    {
        fout.Position = 0;

        var validationFs = new PartitionFileSystem();

        validationFs.Initialize(fout.AsStorage()).ThrowIfFailure();

        return validationFs;
    }
}