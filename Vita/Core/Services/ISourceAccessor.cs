namespace Vita.Core.Services;

public interface ISourceAccessor : IDisposable
{
    bool DirectoryExists(string relativePath);

    IEnumerable<string> EnumerateDirectoryNames(string relativePath);

    IEnumerable<string> EnumerateAllFiles();

    bool FileExists(string relativePath);

    byte[] ReadAllBytes(string relativePath);

    long GetFileSize(string relativePath);
}