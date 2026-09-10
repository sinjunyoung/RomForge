namespace Patch.Core.Services;

public sealed class PatchFileRef
{
    public string? DiskPath { get; }
    private readonly Func<Stream>? _archiveOpener;
    private readonly long _archiveLength;

    private PatchFileRef(string? diskPath, Func<Stream>? archiveOpener, long archiveLength)
    {
        DiskPath = diskPath;
        _archiveOpener = archiveOpener;
        _archiveLength = archiveLength;
    }

    public static PatchFileRef FromDisk(string path) => new(path, null, 0);

    public static PatchFileRef FromArchiveEntry(IArchivePatchEntry entry) => new(null, entry.Open, entry.Length);

    public bool IsDisk => DiskPath != null;

    public string DisplayName => DiskPath != null ? Path.GetFileName(DiskPath) : "(archive)";

    public long Length => IsDisk ? new FileInfo(DiskPath!).Length : _archiveLength;

    public Stream OpenRead() => IsDisk ? File.OpenRead(DiskPath!) : _archiveOpener!();

    public byte[] ReadSmallFileBytes()
    {
        using var s = OpenRead();
        using var ms = new MemoryStream();

        s.CopyTo(ms);

        return ms.ToArray();
    }

    public async Task<byte[]> ReadSmallFileBytesAsync(CancellationToken ct = default)
    {
        await using var s = OpenRead();
        using var ms = new MemoryStream();

        await s.CopyToAsync(ms, ct);

        return ms.ToArray();
    }

    public MaterializedTempFile MaterializeAsTempFile(string suggestedExtension)
    {
        if (IsDisk)
            return MaterializedTempFile.Wrap(DiskPath!);

        string tempPath = Path.Combine(AppContext.BaseDirectory, $"RomForgePatch_{Guid.NewGuid():N}{suggestedExtension}");

        using (var dst = File.Create(tempPath))
        using (var src = OpenRead())
            src.CopyTo(dst);

        return MaterializedTempFile.Owned(tempPath);
    }
}

public readonly struct MaterializedTempFile(string path, bool owned) : IDisposable
{
    public string Path { get; } = path;

    public static MaterializedTempFile Wrap(string path) => new(path, false);

    public static MaterializedTempFile Owned(string path) => new(path, true);

    public void Dispose()
    {
        if (owned && File.Exists(Path))
        {
            try { File.Delete(Path); } catch { }
        }
    }
}