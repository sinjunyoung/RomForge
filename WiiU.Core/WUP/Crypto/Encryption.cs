using System.Diagnostics;
using System.Security.Cryptography;
using WiiU.Core.WUP.Services;

namespace WiiU.Core.WUP.Crypto
{
    public class Encryption
    {
        private Key key = new ();
        private IV iv = new ();

        private readonly Aes aes;

        public Encryption(Key key, IV iv)
        {
            aes = Aes.Create();

            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            aes.KeySize = 128;

            Init(key, iv);
        }

        public void Init(IV iv)
        {
            Init(GetKey(), iv);
        }

        public void Init(Key key)
        {
            Init(key, new IV());
        }

        public void Init(Key key, IV iv)
        {
            SetKey(key);
            SetIV(iv);
            aes.Key = GetKey().GetKey();
            aes.IV = GetIV().GetIV();
        }

        public void EncryptFileWithPadding(FST fst, string output_filename, short contentID, int BLOCKSIZE)
        {
            using Stream in_ = new MemoryStream(fst.GetAsData());
            using FileStream out_ = new (output_filename, FileMode.Create, FileAccess.Write);

            IV ivObj = new (JByteBuffer.Allocate(0x10).PutShort(contentID).Array());

            EncryptSingleFile(in_, out_, fst.GetDataSize(), ivObj, BLOCKSIZE);
        }

        public void EncryptFileWithPadding(string filePath, Content content, string output_filename, int BLOCKSIZE)
        {
            EncryptFileWithPadding(filePath, content, output_filename, BLOCKSIZE, null);
        }

        public void EncryptFileWithPadding(string filePath, Content content, string output_filename, int BLOCKSIZE, Action<long>? onBytesEncrypted)
        {
            using Stream in_ = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using FileStream out_ = new (output_filename, FileMode.Create, FileAccess.Write);

            IV ivObj = new (JByteBuffer.Allocate(0x10).PutShort((short)content.GetID()).Array());

            EncryptSingleFile(in_, out_, new FileInfo(filePath).Length, ivObj, BLOCKSIZE, onBytesEncrypted);
        }

        public void EncryptSingleFile(Stream in_, Stream out_, long length, IV? iv, int BLOCKSIZE)
        {
            EncryptSingleFile(in_, out_, length, iv, BLOCKSIZE, null);
        }

        public void EncryptSingleFile(Stream in_, Stream out_, long length, IV? iv, int BLOCKSIZE, Action<long>? onBytesEncrypted)
        {
            long inputSize = length;
            long targetSize = Services.Utils.Align(inputSize, BLOCKSIZE);
            byte[] blockBuffer = new byte[BLOCKSIZE];
            int inBlockBufferRead;
            long cur_position = 0;
            ByteArrayBuffer overflow = new (BLOCKSIZE);
            bool first = true;

            do
            {
                if (first)
                    first = false;
                else
                    iv = null;

                if (cur_position + BLOCKSIZE > inputSize)
                {
                    long expectedSize = inputSize - cur_position;
                    byte[] buffer = new byte[BLOCKSIZE];

                    inBlockBufferRead = Services.Utils.GetChunkFromStream(in_, blockBuffer, overflow, expectedSize);

                    Array.Copy(blockBuffer, 0, buffer, 0, inBlockBufferRead);

                    blockBuffer = buffer;
                    inBlockBufferRead = BLOCKSIZE;
                }
                else
                {
                    int expectedSize = BLOCKSIZE;

                    inBlockBufferRead = Services.Utils.GetChunkFromStream(in_, blockBuffer, overflow, expectedSize);
                }

                byte[] output = EncryptChunk(blockBuffer, inBlockBufferRead, iv);

                SetIV(new IV(output[(BLOCKSIZE - 16)..BLOCKSIZE]));

                cur_position += inBlockBufferRead;
                out_.Write(output, 0, inBlockBufferRead);
                onBytesEncrypted?.Invoke(cur_position);

            } while (cur_position < targetSize && (inBlockBufferRead == BLOCKSIZE));
        }

        public void EncryptFileHashed(string filePath, Content content, string output_filename, ContentHashes hashes)
        {
            EncryptFileHashed(filePath, content, output_filename, hashes, null);
        }

        public void EncryptFileHashed(string filePath, Content content, string output_filename, ContentHashes hashes, Action<long>? onBytesEncrypted)
        {
            long inputLength = new FileInfo(filePath).Length;

            using (Stream in_ = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (Stream out_ = new FileStream(output_filename, FileMode.Create, FileAccess.Write))
                EncryptFileHashed(in_, out_, inputLength, content, hashes, onBytesEncrypted);

            content.SetEncryptedFileSize(new FileInfo(output_filename).Length);
        }

        private void EncryptFileHashed(Stream in_, Stream out_, long length, Content content, ContentHashes hashes, Action<long>? onBytesEncrypted)
        {
            int BLOCKSIZE = 0x10000;
            int HASHBLOCKSIZE = 0xFC00;
            int buffer_size = HASHBLOCKSIZE;
            byte[] buffer = new byte[buffer_size];
            ByteArrayBuffer overflowbuffer = new (buffer_size);
            int read;
            int block = 0;
            int totalblocks = (int)(length / HASHBLOCKSIZE);

            do
            {
                read = Services.Utils.GetChunkFromStream(in_, buffer, overflowbuffer, buffer_size);

                if (read != buffer_size)
                {
                    byte[] new_buffer = new byte[buffer_size];

                    Array.Copy(buffer, new_buffer, buffer.Length);

                    buffer = new_buffer;
                }

                byte[] output = EncryptChunkHashed(buffer, block, hashes, content.GetID());

                if (output.Length != BLOCKSIZE) 
                    Debug.WriteLine("WTF?");

                out_.Write(output, 0, output.Length);

                block++;

                onBytesEncrypted?.Invoke((long)block * HASHBLOCKSIZE);

            } while (read == buffer_size);
        }

        private byte[] EncryptChunkHashed(byte[] buffer, int block, ContentHashes hashes, int content_id)
        {
            IV ivObj = new (JByteBuffer.Allocate(16).PutShort((short)content_id).Array());
            byte[] decryptedHashes = hashes.GetHashForBlock(block);

            decryptedHashes[1] ^= (byte)content_id;

            byte[] encryptedhashes = EncryptChunk(decryptedHashes, 0x0400, ivObj);

            decryptedHashes[1] ^= (byte)content_id;

            int iv_start = (block % 16) * 20;

            ivObj = new IV(decryptedHashes[iv_start..(iv_start + 16)]);

            byte[] encryptedContent = EncryptChunk(buffer, 0xFC00, ivObj);
            byte[] output = new byte[0x10000];

            Array.Copy(encryptedhashes, 0, output, 0, encryptedhashes.Length);
            Array.Copy(encryptedContent, 0, output, encryptedhashes.Length, encryptedContent.Length);

            return output;
        }

        public byte[] EncryptChunk(byte[] blockBuffer, int BLOCKSIZE, IV? iv)
        {
            return EncryptChunk(blockBuffer, 0, BLOCKSIZE, iv);
        }

        public byte[] EncryptChunk(byte[] blockBuffer, int offset, int BLOCKSIZE, IV? iv)
        {
            if (iv != null)
                SetIV(iv);

            Init(GetIV());

            byte[] output = Encrypt(blockBuffer, offset, BLOCKSIZE);

            return output;
        }

        public byte[] Encrypt(byte[] input)
        {
            return Encrypt(input, input.Length);
        }

        public byte[] Encrypt(byte[] input, int len)
        {
            return Encrypt(input, 0, len);
        }

        public byte[] Encrypt(byte[] input, int offset, int len)
        {
            using ICryptoTransform encryptor = aes.CreateEncryptor();
            return encryptor.TransformFinalBlock(input, offset, len);
        }

        public Key GetKey() => key;

        public void SetKey(Key key)
        {
            this.key = key;
        }

        public IV GetIV() => iv;

        public void SetIV(IV iv)
        {
            this.iv = iv;
        }
    }
}