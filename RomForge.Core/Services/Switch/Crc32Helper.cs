using System.IO;

namespace RomForge.Core.Services.Switch
{
    public static class Crc32Helper
    {
        private static readonly uint[] Table = BuildTable();

        private static uint[] BuildTable()
        {
            var table = new uint[256];

            for (uint i = 0; i < 256; i++)
            {
                uint c = i;

                for (int k = 0; k < 8; k++)
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;

                table[i] = c;
            }

            return table;
        }

        public static uint Compute(byte[] data)
        {
            uint crc = 0xFFFFFFFF;

            foreach (byte b in data)
                crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);

            return crc ^ 0xFFFFFFFF;
        }

        public static uint ComputeFile(string path)
        {
            uint crc = 0xFFFFFFFF;
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            var buffer = new byte[1 << 20];
            int read;

            while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                    crc = Table[(crc ^ buffer[i]) & 0xFF] ^ (crc >> 8);
            }

            return crc ^ 0xFFFFFFFF;
        }

        public static string BuildTitleId(uint crc32)
        {
            string hex = crc32.ToString("X8");

            return $"010{hex}000";
        }
    }
}