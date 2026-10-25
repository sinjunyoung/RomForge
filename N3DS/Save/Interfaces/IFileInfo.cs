namespace N3DS.Save.Interfaces;

public interface IFileInfo
{
    int InfoByteLen { get; }

    uint GetNext();

    void SetNext(uint index);

    void ReadFrom(byte[] buf, int offset);

    void WriteTo(byte[] buf, int offset);

    IFileInfo Clone();
}