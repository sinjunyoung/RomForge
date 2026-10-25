using WiiU.WUP.Crypto;

namespace WiiU.WUP.Services
{
    public class Ticket(long titleID, Key decryptedKey, Key encryptWith)
    {
        private static readonly Random _rng = new();

        public long TitleID { get; set; } = titleID;

        public Key DecryptedKey { get; set; } = decryptedKey;

        public Key EncryptWith { get; set; } = encryptWith;

        public Key EncryptedKey
        {
            get
            {
                JByteBuffer iv = JByteBuffer.Allocate(0x10);

                iv.PutLong(TitleID);

                Encryption encrypt = new(EncryptWith, new IV(iv.Array()));

                return new Key(encrypt.Encrypt(DecryptedKey.Value));
            }
        }

        public byte[] GetAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(0x350);

            buffer.Put(Utils.HexStringToByteArray("00010004"));

            byte[] randomData = new byte[0x100];

            _rng.NextBytes(randomData);

            buffer.Put(randomData);
            buffer.Put(new byte[0x3C]);
            buffer.Put(Utils.HexStringToByteArray("526F6F742D434130303030303030332D58533030303030303063000000000000"));
            buffer.Put(new byte[0x5C]);
            buffer.Put(Utils.HexStringToByteArray("010000"));
            buffer.Put(EncryptedKey.Value);
            buffer.Put(Utils.HexStringToByteArray("000005"));

            randomData = new byte[0x06];

            _rng.NextBytes(randomData);

            buffer.Put(randomData);
            buffer.Put(new byte[0x04]);
            buffer.PutLong(TitleID);
            buffer.Put(Utils.HexStringToByteArray("00000011000000000000000000000005"));
            buffer.Put(new byte[0xB0]);
            buffer.Put(Utils.HexStringToByteArray("00010014000000AC000000140001001400000000000000280000000100000084000000840003000000000000FFFFFF01"));
            buffer.Put(new byte[0x7C]);

            return buffer.Array();
        }
    }
}