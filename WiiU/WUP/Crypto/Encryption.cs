using System.Diagnostics;
using System.Security.Cryptography;
using WiiU.WUP.Services;

namespace WiiU.WUP.Crypto
{
    public class Encryption
    {
        private Key _key = new ();
        private IV _iv = new ();

        private readonly Aes aes;

        public Key Key { get => _key; set => _key = value; }
        public IV IV { get => _iv; set => _iv = value; }

        public Encryption(Key key, IV iv)
        {
            aes = Aes.Create();

            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            aes.KeySize = 128;

            Init(key, iv);
        }

        public void Init(IV iv) => Init(Key, iv);

        public void Init(Key key) => Init(key, new IV());

        public void Init(Key key, IV iv)
        {
            Key = key;
            IV = iv;

            aes.Key = Key.Value;
            aes.IV = IV.Value;
        }

        public void EncryptFileWithPadding(FST fst, string output_filename, short contentID, int blockSize)
        {
            using Stream in_ = new MemoryStream(fst.GetAsData());
            using FileStream out_ = new (output_filename, FileMode.Create, FileAccess.Write);
            IV ivObj = new (JByteBuffer.Allocate(0x10).PutShort(contentID).Array());

            EncryptSingleFile(in_, out_, fst.GetDataSize(), ivObj, blockSize);
        }

        public void EncryptFileWithPadding(string filePath, Content content, string output_filename, int blockSize, Action<long>? onBytesEncrypted)
        {
            using Stream in_ = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using FileStream out_ = new (output_filename, FileMode.Create, FileAccess.Write);
            IV ivObj = new (JByteBuffer.Allocate(0x10).PutShort((short)content.ID).Array());

            EncryptSingleFile(in_, out_, new FileInfo(filePath).Length, ivObj, blockSize, onBytesEncrypted);
        }

        public void EncryptSingleFile(Stream in_, Stream out_, long length, IV? iv, int blockSize) => EncryptSingleFile(in_, out_, length, iv, blockSize, null);

        public void EncryptSingleFile(Stream in_, Stream out_, long length, IV? iv, int blockSize, Action<long>? onBytesEncrypted)
        {
            long inputSize = length;
            long targetSize = Utils.Align(inputSize, blockSize);
            byte[] blockBuffer = new byte[blockSize];
            int inBlockBufferRead;
            long cur_position = 0;
            ByteArrayBuffer overflow = new (blockSize);
            bool first = true;

            do
            {
                if (first)
                    first = false;
                else
                    iv = null;

                if (cur_position + blockSize > inputSize)
                {
                    long expectedSize = inputSize - cur_position;
                    byte[] buffer = new byte[blockSize];

                    inBlockBufferRead = Utils.GetChunkFromStream(in_, blockBuffer, overflow, expectedSize);

                    Array.Copy(blockBuffer, 0, buffer, 0, inBlockBufferRead);

                    blockBuffer = buffer;
                    inBlockBufferRead = blockSize;
                }
                else
                {
                    int expectedSize = blockSize;

                    inBlockBufferRead = Utils.GetChunkFromStream(in_, blockBuffer, overflow, expectedSize);
                }

                byte[] output = EncryptChunk(blockBuffer, inBlockBufferRead, iv);

                IV = new IV(output[(blockSize - 16)..blockSize]);

                cur_position += inBlockBufferRead;

                out_.Write(output, 0, inBlockBufferRead);
                onBytesEncrypted?.Invoke(cur_position);

            } while (cur_position < targetSize && (inBlockBufferRead == blockSize));
        }

        public void EncryptFileHashed(string filePath, Content content, string output_filename, ContentHashes hashes, Action<long>? onBytesEncrypted)
        {
            long inputLength = new FileInfo(filePath).Length;

            using (Stream in_ = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (Stream out_ = new FileStream(output_filename, FileMode.Create, FileAccess.Write))
                EncryptFileHashed(in_, out_, inputLength, content, hashes, onBytesEncrypted);

            content.EncryptedFileSize = new FileInfo(output_filename).Length;
        }

        private void EncryptFileHashed(Stream in_, Stream out_, long length, Content content, ContentHashes hashes, Action<long>? onBytesEncrypted)
        {
            const int blockSize = 0x10000;
            const int hashBlockSize = 0xFC00;

            int buffer_size = hashBlockSize;
            byte[] buffer = new byte[buffer_size];
            ByteArrayBuffer overflowbuffer = new (buffer_size);
            int read;
            int block = 0;
            int totalblocks = (int)(length / hashBlockSize);

            do
            {
                read = Utils.GetChunkFromStream(in_, buffer, overflowbuffer, buffer_size);

                if (read != buffer_size)
                {
                    byte[] new_buffer = new byte[buffer_size];

                    Array.Copy(buffer, new_buffer, buffer.Length);

                    buffer = new_buffer;
                }

                byte[] output = EncryptChunkHashed(buffer, block, hashes, content.ID);

                if (output.Length != blockSize) 
                    Debug.WriteLine("WTF?");

                out_.Write(output, 0, output.Length);

                block++;

                onBytesEncrypted?.Invoke((long)block * hashBlockSize);

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

        public byte[] EncryptChunk(byte[] blockBuffer, int blockSize, IV? iv) => EncryptChunk(blockBuffer, 0, blockSize, iv);

        public byte[] EncryptChunk(byte[] blockBuffer, int offset, int blockSize, IV? iv)
        {
            if (iv != null)
                IV = iv;

            Init(IV);

            byte[] output = Encrypt(blockBuffer, offset, blockSize);

            return output;
        }

        public byte[] Encrypt(byte[] input) => Encrypt(input, input.Length);

        public byte[] Encrypt(byte[] input, int len) => Encrypt(input, 0, len);

        public byte[] Encrypt(byte[] input, int offset, int len)
        {
            using ICryptoTransform encryptor = aes.CreateEncryptor();
            return encryptor.TransformFinalBlock(input, offset, len);
        }
    }
}