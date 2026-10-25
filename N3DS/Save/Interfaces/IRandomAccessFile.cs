namespace N3DS.Save.Interfaces;

public interface IRandomAccessFile
{
    int Length { get; }

    void Read(int pos, byte[] buf, int offset, int count);

    void Write(int pos, byte[] buf, int offset, int count);

    void Commit();
}