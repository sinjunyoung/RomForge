namespace N3DS.Save.Interfaces;

public interface IDirInfo
{
    int InfoByteLen { get; }

    uint GetSubDir();

    void SetSubDir(uint index);

    uint GetSubFile();

    void SetSubFile(uint index);

    uint GetNext();

    void SetNext(uint index);

    void ReadFrom(byte[] buf, int offset);

    void WriteTo(byte[] buf, int offset);

    IDirInfo Clone();
}