namespace N3DS.Save.Interfaces;

public interface IParentedKey
{
    int KeyByteLen { get; }

    uint GetParent();

    void ReadFrom(byte[] buf, int offset);

    void WriteTo(byte[] buf, int offset);

    bool Equals(IParentedKey other);
}