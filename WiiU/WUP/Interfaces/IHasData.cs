namespace WiiU.WUP.Interfaces
{
    public interface IHasData
    {
        byte[] GetAsData();

        int GetDataSize();
    }
}