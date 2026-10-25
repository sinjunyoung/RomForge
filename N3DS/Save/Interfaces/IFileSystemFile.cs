namespace N3DS.Save.Interfaces;

public interface IFileSystemFile
{
    uint GetParentIno();

    uint GetIno();

    void Delete();

    void Resize(int len);

    void Read(int pos, byte[] buf, int offset, int count);

    void Write(int pos, byte[] buf, int offset, int count);

    int Length { get; }

    void Commit();
}