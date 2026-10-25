using Patch.Core.Interfaces;
using SharpCompress.Archives;
using SharpCompress.Readers;

namespace Patch.Core.Services;

public sealed class ZipArchivePatchSource : IArchivePatchSource
{
    private readonly IArchive _archive;
    private readonly Dictionary<string, IArchiveEntry> _byPath;

    public IReadOnlyList<string> EntryPaths { get; }

    public bool SupportsCheapRepeatedOpen => true;

    public ZipArchivePatchSource(string path, string? password = null)
    {
        IArchive? archive = null;

        try
        {
            archive = ArchiveFactory.OpenArchive(path, new ReaderOptions { Password = password });

            _byPath = new Dictionary<string, IArchiveEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory || string.IsNullOrEmpty(entry.Key))
                    continue;

                _byPath[entry.Key.Replace('\\', '/')] = entry;
            }

            var encrypted = _byPath.Values.Where(e => e.IsEncrypted).ToList();

            if (encrypted.Count > 0)
            {
                if (string.IsNullOrEmpty(password))
                    throw new ArchivePasswordRequiredException(path);

                var probe = encrypted.OrderBy(e => e.Size).First();

                using var probeStream = probe.OpenEntryStream();
                probeStream.CopyTo(Stream.Null);
            }

            _archive = archive;
        }
        catch (ArchivePasswordRequiredException)
        {
            archive?.Dispose();
            throw;
        }
        catch (Exception) when (!string.IsNullOrEmpty(password))
        {
            archive?.Dispose();
            throw new ArchivePasswordRequiredException(path);
        }
        catch
        {
            archive?.Dispose();
            throw;
        }

        EntryPaths = [.. _byPath.Keys];
    }

    public IArchivePatchEntry? FindEntry(string path) => _byPath.TryGetValue(path, out var entry) ? new Entry(entry) : null;

    public void Dispose() => _archive.Dispose();

    private sealed class Entry(IArchiveEntry entry) : IArchivePatchEntry
    {
        public string FullPath => entry.Key!.Replace('\\', '/');

        public long Length => entry.Size;

        public Stream Open() => entry.OpenEntryStream();
    }
}