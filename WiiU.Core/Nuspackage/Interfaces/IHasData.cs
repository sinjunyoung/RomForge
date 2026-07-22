namespace NUSPacker.Nuspackage.Interfaces
{
    /// <summary>
    /// A interface for the serialized data
    /// </summary>
    public interface IHasData
    {
        byte[] GetAsData();
        int GetDataSize();
    }
}
