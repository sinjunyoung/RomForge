using Common;
using LibHac.Common.Keys;
using LibHac.Fs.Fsa;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using System.IO;
using FileEntry = (string Name, System.Func<System.IO.Stream, System.Action<long>, System.Threading.Tasks.Task> Writer, long EstimatedSize, string Label);

namespace RomForge.Core.Services.Switch;

internal sealed class XciOutput : OutputContainer
{
    protected override bool IsXci => true;

    public override async Task WriteAsync(SourceSet source, string displayName, string progressId, uint pfsAlignment, List<FileEntry> entries, Stream fout, IProgress<ProgressInfo>? progress, List<IDisposable> disposables, CancellationToken ct = default)
    {
        var baseStream = source.OpenXciBaseStream(disposables);

        await XciWriter.WriteAsync(displayName, progressId, source.BuildXciPrefix(), source.XciRootEntries, baseStream, entries, fout, progress, ct);
    }

    public override IFileSystem OpenForValidation(Stream fout, KeySet keySet)
    {
        fout.Position = 0;

        var validationStorage = new StreamStorage(fout, false);

        return new Xci(keySet, validationStorage).OpenPartition(XciPartitionType.Secure);
    }
}