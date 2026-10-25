using N3DS.Save.Models;

namespace N3DS.Save.Interfaces;

public interface IFileSystem
{
    IFileSystemFile OpenFile(uint ino);

    IFileSystemDir OpenDir(uint ino);

    IFileSystemDir OpenRoot() => OpenDir(1);

    void Commit();

    FsStat Stat();
}