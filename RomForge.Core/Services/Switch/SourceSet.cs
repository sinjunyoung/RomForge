using LibHac.Common.Keys;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using NSW.Core;
using System.IO;
using Path = System.IO.Path;
using XciRootEntry = (string Name, ulong AbsOffset, ulong Size, byte[] Hash, uint HashTargetSize);

namespace RomForge.Core.Services.Switch;

internal sealed class SourceSet(IReadOnlyList<string> paths, bool dropXciUpdatePartition = false)
{
    private string? XciPath => paths.FirstOrDefault(p => Path.GetExtension(p).ToLowerInvariant() is ".xci" or ".xcz");

    public List<XciRootEntry> XciRootEntries { get => field ?? XciWriter.DummyRootEntries(); set; }

    public IFileSystem Open(KeySet keySet, string path, List<IDisposable> disposables)
    {
        var storage = new LocalStorage(path, FileAccess.Read);

        disposables.Add(storage);

        IFileSystem fileSystem;

        if (Path.GetExtension(path).ToLowerInvariant() is ".xci" or ".xcz")
        {
            var xci = new Xci(keySet, storage);
            var rootPartition = xci.OpenPartition(XciPartitionType.Root);

            disposables.Add(rootPartition);

            fileSystem = xci.OpenPartition(XciPartitionType.Secure);
            XciRootEntries ??= ReadRootEntries(rootPartition);
        }
        else
            fileSystem = storage.OpenFileSystem(keySet, path);

        disposables.Add(fileSystem);
        keySet.RegisterTickets(fileSystem);

        return fileSystem;
    }

    public byte[] BuildXciPrefix() => XciWriter.BuildPrefix([.. paths]);

    public Stream? OpenXciBaseStream(List<IDisposable> disposables)
    {
        if (XciPath is not { } xciPath)
            return null;

        var storage = new LocalStorage(xciPath, FileAccess.Read);

        disposables.Add(storage);

        return storage.AsStream();
    }

    private List<XciRootEntry> ReadRootEntries(XciPartition rootPartition)
    {
        return [.. rootPartition
            .EnumerateEntries("/", "*")
            .Where(e => !dropXciUpdatePartition || !string.Equals(e.Name.ToString(), "update", StringComparison.OrdinalIgnoreCase))
            .Select(e =>
            {
                var (absOffset, size, hash, hashTargetSize) = rootPartition.GetEntryInfo(e.Name.ToString());
                return (e.Name.ToString(), (ulong)absOffset, (ulong)size, hash, hashTargetSize);
            })];
    }
}