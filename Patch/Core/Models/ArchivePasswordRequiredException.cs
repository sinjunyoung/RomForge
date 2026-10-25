namespace Patch.Core.Services;

public sealed class ArchivePasswordRequiredException(string archivePath) : Exception($"비밀번호가 필요하거나 올바르지 않습니다: {archivePath}")
{
    public string ArchivePath { get; } = archivePath;
}