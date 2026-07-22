using NUSPacker.Utils;

namespace NUSPacker.Nuspackage.Crypto
{
    public class Key
    {
        private static readonly int LENGTH = 0x10;
        private byte[] key = new byte[LENGTH];

        public Key()
        {
        }

        public Key(byte[] key)
        {
            SetKey(key);
        }

        public Key(string hex) : this(Utils.Utils.HexStringToByteArray(hex))
        {
        }

        public byte[] GetKey() => key;

        public void SetKey(byte[]? key)
        {
            if (key != null && key.Length == GetKey().Length)
            {
                this.key = key;
            }
        }

        public int GetLength() => LENGTH;

        public override string ToString()
        {
            return Utils.Utils.ByteArrayToString(key);
        }
    }
}
