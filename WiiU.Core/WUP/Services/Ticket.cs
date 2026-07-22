using WiiU.Core.WUP.Crypto;

namespace WiiU.Core.WUP.Services
{    
    public class Ticket
    {
        private long titleID;
        private Key decryptedKey = new ();
        private Key encryptWith = new ();

        private static readonly Random rng = new ();

        public Ticket(long titleID, Key decryptedKey, Key encryptWith)
        {
            SetTitleID(titleID);
            SetDecryptedKey(decryptedKey);
            SetEncryptWith(encryptWith);
        }

        public byte[] GetAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(0x350);

            buffer.Put(Utils.HexStringToByteArray("00010004"));
            byte[] randomData = new byte[0x100];
            rng.NextBytes(randomData);
            buffer.Put(randomData);
            buffer.Put(new byte[0x3C]);
            buffer.Put(Utils.HexStringToByteArray("526F6F742D434130303030303030332D58533030303030303063000000000000"));
            buffer.Put(new byte[0x5C]);
            buffer.Put(Utils.HexStringToByteArray("010000"));
            buffer.Put(GetEncryptedKey().GetKey());
            buffer.Put(Utils.HexStringToByteArray("000005"));
            randomData = new byte[0x06];
            rng.NextBytes(randomData);
            buffer.Put(randomData);
            buffer.Put(new byte[0x04]);
            buffer.PutLong(GetTitleID());
            buffer.Put(Utils.HexStringToByteArray("00000011000000000000000000000005"));
            buffer.Put(new byte[0xB0]);
            buffer.Put(Utils.HexStringToByteArray("00010014000000AC000000140001001400000000000000280000000100000084000000840003000000000000FFFFFF01"));
            buffer.Put(new byte[0x7C]);

            return buffer.Array();
        }

        public Key GetEncryptedKey()
        {
            JByteBuffer iv = JByteBuffer.Allocate(0x10);

            iv.PutLong(GetTitleID());

            Encryption encrypt = new (GetEncryptWith(), new IV(iv.Array()));

            return new Key(encrypt.Encrypt(GetDecryptedKey().GetKey()));
        }

        public long GetTitleID() => titleID;

        public void SetTitleID(long titleID)
        {
            this.titleID = titleID;
        }

        public Key GetDecryptedKey() => decryptedKey;

        public void SetDecryptedKey(Key decryptedKey)
        {
            this.decryptedKey = decryptedKey;
        }

        public Key GetEncryptWith() => encryptWith;

        public void SetEncryptWith(Key encryptWith)
        {
            this.encryptWith = encryptWith;
        }
    }
}