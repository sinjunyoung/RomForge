using System;
using System.IO;
using System.Numerics;
using System.Text;

namespace NUSPacker.Utils
{
    public static class Utils
    {
        public static void DeleteDir(string path)
        {
            if (Directory.Exists(path))
            {
                foreach (var dir in Directory.GetDirectories(path))
                {
                    DeleteDir(dir);
                }
                foreach (var file in Directory.GetFiles(path))
                {
                    try { File.Delete(file); } catch { /* best effort, mirrors deleteOnExit */ }
                }
            }
            try
            {
                if (Directory.Exists(path)) Directory.Delete(path, true);
                else if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // Java's deleteOnExit() swallows failures too (deferred to JVM exit); mirror best-effort behavior.
            }
        }

        public static long Align(long input, int alignment)
        {
            long newSize = input / alignment;
            if (newSize * alignment != input)
            {
                newSize++;
            }
            newSize = newSize * alignment;
            return newSize;
        }

        public static byte[] HexStringToByteArray(string s)
        {
            int len = s.Length;
            byte[] data = new byte[len / 2];
            for (int i = 0; i < len; i += 2)
            {
                data[i / 2] = (byte)((HexDigit(s[i]) << 4) + HexDigit(s[i + 1]));
            }
            return data;
        }

        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }

        public static long HexStringToLong(string s)
        {
            try
            {
                // Java's `new BigInteger(s, 16).longValue()` parses s as a nonnegative hex integer
                // (of arbitrary length) and then keeps only the low 64 bits (two's complement).
                // Prefixing with "0" forces System.Numerics.BigInteger to parse it as non-negative.
                BigInteger bi = BigInteger.Parse("0" + s, System.Globalization.NumberStyles.HexNumber);
                ulong low64 = (ulong)(bi & new BigInteger(ulong.MaxValue));
                return unchecked((long)low64);
            }
            catch (Exception)
            {
                return 0L;
            }
        }

        public static string ByteArrayToString(byte[]? ba)
        {
            if (ba == null) return null!;
            var hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
            {
                hex.Append(b.ToString("X2"));
            }
            return hex.ToString();
        }

        public static int GetChunkFromStream(Stream inputStream, byte[] output, ByteArrayBuffer overflowbuffer, long expectedSize)
        {
            int bytesRead;
            int inBlockBuffer = 0;
            do
            {
                bytesRead = inputStream.Read(overflowbuffer.buffer, overflowbuffer.GetLengthOfDataInBuffer(), overflowbuffer.GetSpaceLeft());
                if (bytesRead <= 0) break;

                overflowbuffer.AddLengthOfDataInBuffer(bytesRead);

                if (inBlockBuffer + overflowbuffer.GetLengthOfDataInBuffer() > expectedSize)
                {
                    long tooMuch = (inBlockBuffer + bytesRead) - expectedSize;
                    long toRead = expectedSize - inBlockBuffer;

                    Array.Copy(overflowbuffer.buffer, 0, output, inBlockBuffer, (int)toRead);
                    inBlockBuffer += (int)toRead;

                    Array.Copy(overflowbuffer.buffer, (int)toRead, overflowbuffer.buffer, 0, (int)tooMuch);
                    overflowbuffer.SetLengthOfDataInBuffer((int)tooMuch);
                }
                else
                {
                    Array.Copy(overflowbuffer.buffer, 0, output, inBlockBuffer, overflowbuffer.GetLengthOfDataInBuffer());
                    inBlockBuffer += overflowbuffer.GetLengthOfDataInBuffer();
                    overflowbuffer.ResetLengthOfDataInBuffer();
                }
            } while (inBlockBuffer != expectedSize);
            return inBlockBuffer;
        }

        public static long GetUnsingedIntFromBytes(byte[] input, int offset)
        {
            uint v = (uint)((input[offset] << 24) | (input[offset + 1] << 16) | (input[offset + 2] << 8) | input[offset + 3]);
            return v;
        }

        public static int GetIntFromBytes(byte[] input, int offset)
        {
            return (input[offset] << 24) | (input[offset + 1] << 16) | (input[offset + 2] << 8) | input[offset + 3];
        }

        public static long CopyFileInto(string filePath, Stream out_)
        {
            return CopyFileInto(filePath, out_, null);
        }

        public static long CopyFileInto(string filePath, Stream out_, string? output)
        {
            if (output != null)
            {
                Console.Write(output);
            }
            using FileStream in_ = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            long written = 0;
            long filesize = new FileInfo(filePath).Length;
            int buffer_size = 0x10000;
            byte[] buffer = new byte[buffer_size];
            long cycle = 0;
            do
            {
                int read = in_.Read(buffer, 0, buffer.Length);
                if (read <= 0) break;
                out_.Write(buffer, 0, read);
                written += read;
                if ((cycle % 10) == 0 && output != null)
                {
                    int progress = filesize == 0 ? 100 : (int)((written * 1.0 / filesize * 1.0) * 100);
                    Console.Write("\r" + output + ": " + progress + "%");
                }
                cycle++;
            } while (written < filesize);
            if (output != null)
            {
                Console.WriteLine("\r" + output + ": 100%");
            }
            return written;
        }
    }
}
