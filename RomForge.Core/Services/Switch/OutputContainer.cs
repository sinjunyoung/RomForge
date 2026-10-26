using Common;
using LibHac.Common.Keys;
using LibHac.Fs.Fsa;
using NSW.Core.Enums;
using System.IO;
using FileEntry = (string Name, System.Func<System.IO.Stream, System.Action<long>, System.Threading.Tasks.Task> Writer, long EstimatedSize, string Label);

namespace RomForge.Core.Services.Switch;

internal abstract class OutputContainer
{
    public static readonly OutputContainer AsNsp = new NspOutput();
    public static readonly OutputContainer AsXci = new XciOutput();

    protected abstract bool IsXci { get; }

    public string Extension(bool compressed) => IsXci ? (compressed ? ".xcz" : ".xci") : (compressed ? ".nsz" : ".nsp");

    public ContainerFormat Format(bool compressed) => IsXci ? (compressed ? ContainerFormat.Xcz : ContainerFormat.Xci) : (compressed ? ContainerFormat.Nsz : ContainerFormat.Nsp);

    public abstract Task WriteAsync(SourceSet source, string displayName, string progressId, uint pfsAlignment, List<FileEntry> entries, Stream fout, IProgress<ProgressInfo> progress, List<IDisposable> disposables, CancellationToken ct);

    public abstract IFileSystem OpenForValidation(Stream fout, KeySet keySet);
}