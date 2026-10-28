namespace WiiU.WUP.Crypto
{
    public class Key
    {
        private static readonly int Length = 0x10;

        private byte[] _value = new byte[Length];

        public byte[] Value 
        { 
            get => _value;
            private set
            {
                if (value != null && value.Length == Value.Length)
                    _value = value;
            }
            
        }

        public Key() { }

        public Key(byte[] key) => Value = key;

        public Key(string hex) : this(Services.Utils.HexStringToByteArray(hex)) { }

        public override string ToString() => Services.Utils.ByteArrayToString(Value);
    }
}