using Common.Localization;

namespace Patch.Core.Models;

public sealed class ArchivePasswordRequiredException(string archivePath) : Exception(Loc.S.ErrArchivePasswordRequired(archivePath))
{
    public string ArchivePath { get; } = archivePath;
}