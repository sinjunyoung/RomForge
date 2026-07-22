using System.Security.Cryptography;

namespace WiiU.Core.WUP.Services
{
    public static class HashUtil
    {
        public static byte[] HashSHA2(byte[] data) => SHA256.HashData(data);

        public static byte[] HashSHA1(byte[] data) => SHA1.HashData(data);

        public static byte[] HashSHA1(string filePath) => HashSHA1(filePath, 0);

        public static byte[] HashSHA1(string filePath, int alignment)
        {
            byte[] hash = new byte[0x14];

            try
            {
                using FileStream in_ = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using SHA1 sha1 = SHA1.Create();
                hash = Hash(sha1, in_, new FileInfo(filePath).Length, 0x8000, alignment);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
            }

            return hash;
        }

        public static byte[] Hash(HashAlgorithm digest, Stream in_, long inputSize, int bufferSize, int alignment)
        {
            long target_size = alignment == 0 ? inputSize : Utils.Align(inputSize, alignment);
            long cur_position = 0;
            int inBlockBufferRead;
            byte[] blockBuffer = new byte[bufferSize];
            ByteArrayBuffer overflow = new ByteArrayBuffer(bufferSize);

            do
            {
                if (cur_position + bufferSize > inputSize)
                {
                    long expectedSize = inputSize - cur_position;
                    byte[] buffer = new byte[bufferSize];

                    inBlockBufferRead = Utils.GetChunkFromStream(in_, blockBuffer, overflow, expectedSize);

                    Array.Copy(blockBuffer, 0, buffer, 0, inBlockBufferRead);

                    blockBuffer = buffer;
                    inBlockBufferRead = bufferSize;
                }
                else
                {
                    int expectedSize = bufferSize;

                    inBlockBufferRead = Utils.GetChunkFromStream(in_, blockBuffer, overflow, expectedSize);
                }

                digest.TransformBlock(blockBuffer, 0, inBlockBufferRead, null, 0);
                cur_position += inBlockBufferRead;

            } while (cur_position < target_size && (inBlockBufferRead == bufferSize));

            digest.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

            return digest.Hash!;
        }
    }
}