namespace Patch.Core.Services;

public sealed class NestedArchivePatchSource : IArchivePatchSource
{
    private static readonly string[] NestedExtensions = [".zip", ".7z"];

    private readonly List<IArchivePatchSource> _ownedSources = [];
    private readonly List<MaterializedTempFile> _tempFiles = [];
    private readonly Dictionary<string, (IArchivePatchSource Source, string LocalPath)> _byFlatPath = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> EntryPaths { get; }

    public bool SupportsCheapRepeatedOpen => false;

    public NestedArchivePatchSource(IArchivePatchSource root, int maxDepth = 4)
    {
        _ownedSources.Add(root);

        Expand(root, string.Empty, maxDepth, isRoot: true);
        EntryPaths = [.. _byFlatPath.Keys];
    }

    private void Expand(IArchivePatchSource source, string prefix, int depthRemaining, bool isRoot)
    {
        if (!isRoot)
            _ownedSources.Add(source);

        foreach (string path in source.EntryPaths)
        {
            string flatPath = prefix.Length == 0 ? path : $"{prefix}/{path}";
            string ext = Path.GetExtension(path);
            bool isNestedArchive = NestedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);

            if (isNestedArchive && depthRemaining > 0)
            {
                var entry = source.FindEntry(path);

                if (entry == null)
                {
                    _byFlatPath[flatPath] = (source, path);
                    continue;
                }

                var patchRef = PatchFileRef.FromArchiveEntry(entry);
                var tempFile = patchRef.MaterializeAsTempFile(ext);
                _tempFiles.Add(tempFile);

                try
                {
                    var nested = ArchivePatchSourceFactory.OpenRaw(tempFile.Path);
                    Expand(nested, flatPath, depthRemaining - 1, isRoot: false);
                }
                catch
                {
                    _byFlatPath[flatPath] = (source, path);
                }
            }
            else
            {
                _byFlatPath[flatPath] = (source, path);
            }
        }
    }

    public IArchivePatchEntry? FindEntry(string path) =>
        _byFlatPath.TryGetValue(path, out var loc) ? loc.Source.FindEntry(loc.LocalPath) : null;

    public void Dispose()
    {
        foreach (var s in _ownedSources)
            s.Dispose();

        foreach (var t in _tempFiles)
            t.Dispose();
    }
}