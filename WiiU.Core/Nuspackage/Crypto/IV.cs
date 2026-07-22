namespace NUSPacker.Nuspackage.Crypto
{
    public class IV
    {
        private static readonly int LENGTH = 0x10;
        private byte[] iv = new byte[LENGTH];

        public IV()
        {
        }

        public IV(byte[] array)
        {
            SetIV(array);
        }

        public byte[] GetIV() => iv;

        public void SetIV(byte[]? iv)
        {
            if (iv != null && iv.Length == GetIV().Length)
            {
                this.iv = iv;
            }
        }

        public int GetLength() => LENGTH;
    }
}
