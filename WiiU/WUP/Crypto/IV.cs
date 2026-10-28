namespace WiiU.WUP.Crypto
{
    public class IV
    {
        private static readonly int Length = 0x10;
        private byte[] _value = new byte[Length];

        public byte[] Value 
        { 
            get => _value;
            private set
            {
                if (value != null && value.Length == _value.Length)
                    _value = value;
            }
        }

        public IV() { }

        public IV(byte[] array) => Value = array;
    }
}