using WiiU.Core.WUP.NusPackage.Interfaces;

namespace WiiU.Core.WUP.Services
{
    public class ContentInfos : IHasData
    {
        private const int contentInfoCount = 0x40;

        private readonly ContentInfo?[] contentinfos = new ContentInfo?[contentInfoCount];

        public ContentInfos()
        {
        }

        public void SetContentInfo(int index, ContentInfo contentInfo)
        {
            if (index < 0 && index > (contentinfos.Length - 1))
                throw new ArgumentException("Error on setting ContentInfo, index " + index + " invalid");

            contentinfos[index] = contentInfo ?? throw new ArgumentException("Error on setting ContentInfo, ContentInfo is null");
        }

        public ContentInfo GetContentInfo(int index)
        {
            if (index < 0 && index > (contentinfos.Length - 1))
                throw new ArgumentException("Error on getting ContentInfo, index " + index + " invalid");

            if (contentinfos[index] == null)
                contentinfos[index] = new ContentInfo();

            return contentinfos[index]!;
        }

        public byte[] GetAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(ContentInfo.GetDataSizeStatic() * contentinfos.Length);

            for (int i = 0; i < contentinfos.Length - 1; i++)
            {
                if (contentinfos[i] == null) 
                    contentinfos[i] = new ContentInfo();

                buffer.Put(contentinfos[i]!.GetAsData());
            }

            return buffer.Array();
        }

        public int GetDataSize()
        {
            return contentinfos.Length * ContentInfo.GetDataSizeStatic();
        }
    }
}