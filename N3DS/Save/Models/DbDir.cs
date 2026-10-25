using N3DS.Save;
using N3DS.Save.Interfaces;

namespace N3DS.Save.Models;

public class DbDir(DbInner center, DirMeta meta) : IFileSystemDir
{
    public readonly DirMeta Meta = meta;

    public uint GetParentIno() => Meta.GetParentIno();

    public uint GetIno() => Meta.Ino;

    public IFileSystemDir OpenSubDir(ulong name) => throw new FileNotFoundException("DbDir: title database has no sub directories");

    public IFileSystemFile OpenSubFile(ulong name)
    {
        var key = DbFileKey.New(Meta.Ino, name);
        var fileMeta = Meta.OpenSubFile(key);

        return DbFile.FromMeta(center, fileMeta);
    }

    public List<(ulong name, uint ino)> ListSubDir() => [];

    public List<(ulong name, uint ino)> ListSubFile()
    {
        var raw = Meta.ListSubFile();
        var result = new List<(ulong, uint)>(raw.Count);

        foreach (var (key, ino) in raw)
            result.Add((((DbFileKey)key).Name, ino));

        return result;
    }

    public IFileSystemDir NewSubDir(ulong name) => throw new InvalidOperationException("DbDir: title database does not support sub directories");

    public IFileSystemFile NewSubFile(ulong name, int len)
    {
        try 
        {
            OpenSubFile(name); 
            throw new InvalidOperationException("DbDir::new_sub_file: already exists");
        }
        catch (FileNotFoundException) { }

        FatFile? fatFile;
        uint block;

        if (len == 0)
        {
            fatFile = null;
            block = 0x80000000;
        }
        else
        {
            var (f, b) = center.Fat.Create(Misc.DivideUp(len, center.BlockLen));

            fatFile = f;
            block = (uint)b;
        }

        var key = DbFileKey.New(Meta.Ino, name);
        var info = new DbFileInfo { Next = 0, Padding1 = 0, Block = block, Size = (ulong)len, Padding2 = 0 };

        try
        {
            var fileMeta = Meta.NewSubFile(key, info);

            return DbFile.FromMeta(center, fileMeta);
        }
        catch
        {
            fatFile?.Delete();
            throw;
        }
    }

    public void Delete() => throw new InvalidOperationException("DbDir: cannot delete root");
}