namespace WiiU.Core.WUP.NusPackage.Interfaces
{
    public interface IHasData
    {
        byte[] GetAsData();

        int GetDataSize();
    }
}