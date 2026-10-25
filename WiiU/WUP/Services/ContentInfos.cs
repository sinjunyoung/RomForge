using WiiU.WUP.Interfaces;

namespace WiiU.WUP.Services
{
    public class ContentInfos : IHasData
    {
        private const int ContentInfoCount = 0x40;
        private readonly ContentInfo?[] _contentInfos = new ContentInfo?[ContentInfoCount];

        public ContentInfos() { }

        public ContentInfo this[int index]
        {
            get
            {
                if (index < 0 || index >= _contentInfos.Length)
                    throw new ArgumentOutOfRangeException(nameof(index), $"Error on getting ContentInfo, index {index} invalid");

                return _contentInfos[index] ??= new ContentInfo();
            }
            set
            {
                if (index < 0 || index >= _contentInfos.Length)
                    throw new ArgumentOutOfRangeException(nameof(index), $"Error on setting ContentInfo, index {index} invalid");

                _contentInfos[index] = value ?? throw new ArgumentNullException(nameof(value), "Error on setting ContentInfo, ContentInfo is null");
            }
        }

        public byte[] GetAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(ContentInfo.GetDataSizeStatic() * _contentInfos.Length);

            for (int i = 0; i < _contentInfos.Length; i++)
            {
                if (_contentInfos[i] == null)
                    _contentInfos[i] = new ContentInfo();

                buffer.Put(_contentInfos[i]!.GetAsData());
            }

            return buffer.Array();
        }

        public int GetDataSize() => _contentInfos.Length * ContentInfo.GetDataSizeStatic();
    }
}