using Patch.Core.Interfaces;

namespace Patch.Core.Services;

public sealed class PatchSource : IDisposable
{
    private readonly IArchivePatchSource? _archive;

    public PatchFileIndex Index { get; }

    private PatchSource(PatchFileIndex index, IArchivePatchSource? archive)
    {
        Index = index;
        _archive = archive;
    }

    public static PatchSource Open(string path, params string[] anchors)
    {
        if (!ArchivePatchSourceFactory.IsArchivePath(path))
        {
            string root = PatchFolderResolver.FindPatchRoot(path, anchors) ?? path;
            return new PatchSource(PatchFileIndex.Build(root), null);
        }

        var archive = ArchivePatchSourceFactory.Open(path);

        try
        {
            string prefix = ArchivePatchFolderResolver.FindPatchRoot(archive.EntryPaths, anchors) ?? string.Empty;
            return new PatchSource(PatchFileIndex.Build(archive, prefix), archive);
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    public void Dispose() => _archive?.Dispose();
}