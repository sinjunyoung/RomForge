namespace Vita.Core.Services;

public sealed class FolderSourceAccessor(string root) : IVitaSourceAccessor
{
    private string Full(string relativePath) => Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));

    public bool DirectoryExists(string relativePath) => Directory.Exists(Full(relativePath));

    public IEnumerable<string> EnumerateDirectoryNames(string relativePath) => Directory.Exists(Full(relativePath)) ? Directory.EnumerateDirectories(Full(relativePath)).Select(d => Path.GetFileName(d)!) : [];

    public IEnumerable<string> EnumerateAllFiles() => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(root, f));

    public bool FileExists(string relativePath) => File.Exists(Full(relativePath));

    public byte[] ReadAllBytes(string relativePath) => File.ReadAllBytes(Full(relativePath));

    public void Dispose()
    {
    }
}