using WiiU.WUP.Interfaces;

namespace WiiU.WUP.Services
{
    public class ContentInfo(short indexOffset, short contentCount) : IHasData
    {
        public ContentInfo() : this(0, 0) { }

        public ContentInfo(short contentCount) : this(0, contentCount) { }

        public short IndexOffset { get; set; } = indexOffset;

        public short CommandCount { get; set; } = contentCount;

        public byte[] SHA2Hash { get; set; } = new byte[0x20];

        public byte[] GetAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(0x24);

            buffer.PutShort(IndexOffset);
            buffer.PutShort(CommandCount);
            buffer.Put(SHA2Hash);

            return buffer.Array();
        }

        public static int GetDataSizeStatic() => 0x24;

        public int GetDataSize() => 0x24;
    }
}