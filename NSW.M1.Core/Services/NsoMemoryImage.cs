namespace NSW.M1.Core.Services;

public static class NsoMemoryImage
{
    private const int HeaderSize = 0x100;
    private const int PageSize = 0x1000;

    private static readonly int[] FileOffFields = [0x10, 0x20, 0x30];
    private static readonly int[] MemOffFields = [0x14, 0x24, 0x34];
    private static readonly int[] SizeFields = [0x18, 0x28, 0x38];
    private static readonly int[] FileSizeFields = [0x60, 0x64, 0x68];

    public static byte[] Apply(byte[] plain, Func<byte[], byte[]> applyPatch)
    {
        var fileOff = new int[3];
        var memOff = new int[3];
        var size = new int[3];
        var limit = new int[3];

        for (int i = 0; i < 3; i++)
        {
            fileOff[i] = BitConverter.ToInt32(plain, FileOffFields[i]);
            memOff[i] = BitConverter.ToInt32(plain, MemOffFields[i]);
            size[i] = BitConverter.ToInt32(plain, SizeFields[i]);
        }

        for (int i = 0; i < 3; i++)
        {
            int aligned = (size[i] + PageSize - 1) & ~(PageSize - 1);

            limit[i] = i < 2 ? Math.Min(aligned, memOff[i + 1] - memOff[i]) : aligned;
        }

        byte[] image = new byte[HeaderSize + memOff[2] + limit[2]];

        Array.Copy(plain, 0, image, 0, HeaderSize);

        for (int i = 0; i < 3; i++)
            Array.Copy(plain, fileOff[i], image, HeaderSize + memOff[i], size[i]);

        byte[] patched = applyPatch(image);

        var newSize = new int[3];

        for (int i = 0; i < 3; i++)
        {
            newSize[i] = size[i];

            int start = HeaderSize + memOff[i];

            for (int p = limit[i] - 1; p >= size[i]; p--)
            {
                if (patched[start + p] != 0)
                {
                    newSize[i] = p + 1;
                    break;
                }
            }
        }

        byte[] output = new byte[HeaderSize + newSize.Sum()];

        Array.Copy(plain, 0, output, 0, HeaderSize);

        int cursor = HeaderSize;

        for (int i = 0; i < 3; i++)
        {
            Array.Copy(patched, HeaderSize + memOff[i], output, cursor, newSize[i]);
            BitConverter.GetBytes(cursor).CopyTo(output, FileOffFields[i]);
            BitConverter.GetBytes(newSize[i]).CopyTo(output, SizeFields[i]);
            BitConverter.GetBytes(newSize[i]).CopyTo(output, FileSizeFields[i]);

            cursor += newSize[i];
        }

        return output;
    }
}