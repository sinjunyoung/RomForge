namespace Patch.Core.Interfaces;

public interface IArchivePatchSource : IDisposable
{
    IReadOnlyList<string> EntryPaths { get; }

    IArchivePatchEntry? FindEntry(string path);

    bool SupportsCheapRepeatedOpen { get; }
}