namespace N3DS.Save.Interfaces;

public interface IFileSystemDir
{
    uint GetParentIno();

    uint GetIno();

    IFileSystemDir OpenSubDir(ulong name);

    IFileSystemFile OpenSubFile(ulong name);

    List<(ulong name, uint ino)> ListSubDir();

    List<(ulong name, uint ino)> ListSubFile();

    IFileSystemDir NewSubDir(ulong name);

    IFileSystemFile NewSubFile(ulong name, int len);

    void Delete();
}