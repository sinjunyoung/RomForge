namespace WiiU.Core.WUP.Services
{
    public sealed class JByteBuffer
    {
        private readonly byte[] _buf;
        private int _pos;

        private JByteBuffer(int capacity)
        {
            _buf = new byte[capacity];
            _pos = 0;
        }

        public static JByteBuffer Allocate(int capacity) => new(capacity);

        public int Position
        {
            get => _pos;
            set => _pos = value;
        }

        public int Capacity => _buf.Length;

        public byte[] Array() => _buf;

        public JByteBuffer Clear()
        {
            _pos = 0;

            return this;
        }

        public JByteBuffer Put(byte b)
        {
            _buf[_pos] = b;
            _pos += 1;

            return this;
        }

        public JByteBuffer Put(byte[] src)
        {
            System.Array.Copy(src, 0, _buf, _pos, src.Length);
            _pos += src.Length;

            return this;
        }

        public JByteBuffer PutShort(short v)
        {
            WriteBE(_pos, (long)(ushort)v, 2);
            _pos += 2;

            return this;
        }

        public JByteBuffer PutInt(int v)
        {
            WriteBE(_pos, (long)(uint)v, 4);
            _pos += 4;

            return this;
        }

        public JByteBuffer PutLong(long v)
        {
            WriteBE(_pos, v, 8);
            _pos += 8;

            return this;
        }

        public JByteBuffer Put(int index, byte b)
        {
            _buf[index] = b;

            return this;
        }

        public JByteBuffer PutShort(int index, short v)
        {
            WriteBE(index, (long)(ushort)v, 2);

            return this;
        }

        public JByteBuffer PutInt(int index, int v)
        {
            WriteBE(index, (long)(uint)v, 4);

            return this;
        }

        public JByteBuffer PutLong(int index, long v)
        {
            WriteBE(index, v, 8);

            return this;
        }

        private void WriteBE(int index, long value, int numBytes)
        {
            for (int i = 0; i < numBytes; i++)
                _buf[index + i] = (byte)((value >> (8 * (numBytes - 1 - i))) & 0xFF);
        }
    }
}