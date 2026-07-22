using WiiU.Core.WUP.NusPackage.Interfaces;

namespace WiiU.Core.WUP.Services
{
    public class ContentInfo(short indexOffset, short contentCount) : IHasData
    {
        private short indexOffset = indexOffset;
        private byte[] SHA2Hash = new byte[0x20];

        public ContentInfo() : this((short)0)
        {
        }

        public ContentInfo(short contentCount) : this((short)0, contentCount)
        {
        }

        public byte[] GetAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(0x24);

            buffer.PutShort(GetIndexOffset());
            buffer.PutShort(GetCommandCount());
            buffer.Put(GetSHA2Hash());

            return buffer.Array();
        }

        public static int GetDataSizeStatic() => 0x24;

        public int GetDataSize() => 0x24;

        public short GetCommandCount() => contentCount;

        public short GetIndexOffset() => indexOffset;

        public void SetIndexOffset(short indexOffset)
        {
            this.indexOffset = indexOffset;
        }

        public byte[] GetSHA2Hash() => SHA2Hash;

        public void SetSHA2Hash(byte[] SHA2Hash)
        {
            this.SHA2Hash = SHA2Hash;
        }

        public void SetCommandCount(short commandCount)
        {
            contentCount = commandCount;
        }
    }
}