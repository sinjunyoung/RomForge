using System.IO;

namespace NSW.HacPack;

public interface IFileStore
{
    IEnumerable<string> EnumerateRelativeFiles();

    bool Exists(string relativePath);

    Stream OpenRead(string relativePath);

    byte[] ReadAllBytes(string relativePath);

    void WriteAllBytes(string relativePath, byte[] data);
}