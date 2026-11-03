using System.IO;
using Path = System.IO.Path;

namespace NSW.HacPack;

public sealed class DiskFileStore(string directory) : IFileStore
{
    private readonly string _directory = directory;

    public IEnumerable<string> EnumerateRelativeFiles() => Directory.EnumerateFiles(_directory, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(_directory, f).Replace('\\', '/'));

    public bool Exists(string relativePath) => File.Exists(ResolvePath(relativePath));

    public Stream OpenRead(string relativePath) => new FileStream(ResolvePath(relativePath), FileMode.Open, FileAccess.Read, FileShare.Read);

    public byte[] ReadAllBytes(string relativePath) => File.ReadAllBytes(ResolvePath(relativePath));

    public void WriteAllBytes(string relativePath, byte[] data) => File.WriteAllBytes(ResolvePath(relativePath), data);

    private string ResolvePath(string relativePath) => Path.Combine(_directory, relativePath.Replace('/', Path.DirectorySeparatorChar));
}