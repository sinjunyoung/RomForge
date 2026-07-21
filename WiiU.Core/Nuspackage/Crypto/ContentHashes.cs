using System;
using System.Collections.Generic;
using System.IO;
using NUSPacker.Nuspackage.Contents;
using NUSPacker.Utils;

namespace NUSPacker.Nuspackage.Crypto
{
    public class ContentHashes
    {
        private readonly SortedDictionary<int, byte[]> h0hashes = new SortedDictionary<int, byte[]>();
        private readonly SortedDictionary<int, byte[]> h1hashes = new SortedDictionary<int, byte[]>();
        private readonly SortedDictionary<int, byte[]> h2hashes = new SortedDictionary<int, byte[]>();
        private readonly SortedDictionary<int, byte[]> h3hashes = new SortedDictionary<int, byte[]>();

        /// <summary>Hash for the TMD</summary>
        private byte[] TMDHash = new byte[0x14];

        private int blockCount = 0;

        public ContentHashes(string filePath, bool hashed) : this(filePath, hashed, null)
        {
        }

        /// <param name="onBytesHashed">Optional: invoked after each block is hashed, with cumulative bytes hashed so far. Purely observational.</param>
        public ContentHashes(string filePath, bool hashed, Action<long>? onBytesHashed)
        {
            if (hashed)
            {
                try
                {
                    CalculateH0Hashes(filePath, onBytesHashed);
                    CalculateOtherHashes(1, h0hashes, h1hashes);
                    CalculateOtherHashes(2, h1hashes, h2hashes);
                    CalculateOtherHashes(3, h2hashes, h3hashes);
                    SetTMDHash(HashUtil.HashSHA1(GetH3Hashes()));
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                }
            }
            else
            {
                SetTMDHash(HashUtil.HashSHA1(filePath, Content.CONTENT_FILE_PADDING));
            }
        }

        private void CalculateOtherHashes(int hash_level, SortedDictionary<int, byte[]> in_hashes, SortedDictionary<int, byte[]> out_hashes)
        {
            int hash_level_pow = (int)Math.Pow(16, hash_level);

            int hashescount = (blockCount / hash_level_pow) + 1;
            int new_blocks = 0;
            for (int j = 0; j < hashescount; j++)
            {
                byte[] cur_hashes = new byte[16 * 20];
                for (int i = j * 16; i < (j * 16) + 16; i++)
                {
                    if (in_hashes.TryGetValue(i, out byte[]? cur_hash))
                    {
                        Array.Copy(cur_hash!, 0, cur_hashes, (i % 16) * 20, 20);
                    }
                    else
                    {
                        Array.Copy(new byte[20], 0, cur_hashes, (i % 16) * 20, 20);
                    }
                }
                out_hashes[new_blocks] = HashUtil.HashSHA1(cur_hashes);
                new_blocks++;

                int progress = (int)((new_blocks * 1.0 / hashescount * 1.0) * 100);
                if (new_blocks % 100 == 0)
                {
                    Console.Write("\rcalculating h" + hash_level + ": " + progress + "%");
                }
            }
            Console.WriteLine("\rcalculating h" + hash_level + ": done");
        }

        private void CalculateH0Hashes(string filePath, Action<long>? onBytesHashed)
        {
            using FileStream in_ = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            int buffer_size = 0xFC00;
            byte[] buffer = new byte[buffer_size];
            ByteArrayBuffer overflowbuffer = new ByteArrayBuffer(buffer_size);
            int read;
            int block = 0;

            int total_blocks = (int)(new FileInfo(filePath).Length / buffer_size) + 1;

            do
            {
                read = Utils.Utils.GetChunkFromStream(in_, buffer, overflowbuffer, buffer_size);
                if (read != buffer_size)
                {
                    byte[] new_buffer = new byte[buffer_size];
                    Array.Copy(buffer, new_buffer, buffer.Length);
                    buffer = new_buffer;
                }
                h0hashes[block] = HashUtil.HashSHA1(buffer);

                block++;
                onBytesHashed?.Invoke((long)block * buffer_size);
                int progress = (int)((block * 1.0 / total_blocks * 1.0) * 100);
                if (block % 100 == 0)
                {
                    Console.Write("\rcalculating h0: " + progress + "%");
                }

            } while (read == buffer_size);
            Console.WriteLine("\rcalculating h0: done");
            SetBlockCount(block);
        }

        public byte[] GetHashForBlock(int block)
        {
            if (block > blockCount)
            {
                throw new Exception("fofof");
            }
            JByteBuffer hashes = JByteBuffer.Allocate(0x400);
            int h0_hash_start = (block / 16) * 16;

            for (int i = 0; i < 16; i++)
            {
                int index = h0_hash_start + i;
                if (h0hashes.TryGetValue(index, out byte[]? h))
                {
                    hashes.Put(h!);
                }
                else
                {
                    hashes.Put(new byte[20]);
                }
            }

            int h1_hash_start = (block / 256) * 16;
            for (int i = 0; i < 16; i++)
            {
                int index = h1_hash_start + i;
                if (h1hashes.TryGetValue(index, out byte[]? h))
                {
                    hashes.Put(h!);
                }
                else
                {
                    hashes.Put(new byte[20]);
                }
            }

            int h2_hash_start = (block / 4096) * 16;
            for (int i = 0; i < 16; i++)
            {
                int index = h2_hash_start + i;
                if (h2hashes.TryGetValue(index, out byte[]? h))
                {
                    hashes.Put(h!);
                }
                else
                {
                    hashes.Put(new byte[20]);
                }
            }
            return hashes.Array();
        }

        public int GetBlockCount() => blockCount;

        public void SetBlockCount(int blockCount)
        {
            this.blockCount = blockCount;
        }

        public byte[] GetH3Hashes()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(h3hashes.Count * 0x14);
            for (int i = 0; i < h3hashes.Count; i++)
            {
                buffer.Put(h3hashes[i]);
            }
            return buffer.Array();
        }

        public byte[] GetTMDHash() => TMDHash;

        public void SetTMDHash(byte[] TMDHash)
        {
            this.TMDHash = TMDHash;
        }

        public void SaveH3ToFile(string h3_path)
        {
            if (h3hashes.Count != 0)
            {
                using FileStream fos = new FileStream(h3_path, FileMode.Create, FileAccess.Write);
                byte[] data = GetH3Hashes();
                fos.Write(data, 0, data.Length);
            }
        }
    }
}
