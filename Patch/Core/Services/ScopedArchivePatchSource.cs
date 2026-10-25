using Patch.Core.Interfaces;

namespace Patch.Core.Services;

public sealed class ScopedArchivePatchSource(IArchivePatchSource inner, string scopePrefix) : IArchivePatchSource
{
    private readonly string _prefix = scopePrefix.Length == 0 || scopePrefix.EndsWith('/') ? scopePrefix : scopePrefix + "/";

    public IReadOnlyList<string> EntryPaths { get; } = BuildEntryPaths(inner, scopePrefix.Length == 0 || scopePrefix.EndsWith('/') ? scopePrefix : scopePrefix + "/");

    public bool SupportsCheapRepeatedOpen => inner.SupportsCheapRepeatedOpen;

    public IArchivePatchEntry? FindEntry(string path) => inner.FindEntry(_prefix + path);

    public void Dispose() => inner.Dispose();

    private static IReadOnlyList<string> BuildEntryPaths(IArchivePatchSource inner, string normalizedPrefix) =>
        [.. inner.EntryPaths
            .Where(p => p.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(p => p[normalizedPrefix.Length..])
            .Where(p => p.Length > 0)];
}