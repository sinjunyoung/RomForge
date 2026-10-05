using System.Buffers.Binary;
using System.Text;
using WiiGC.Core.Models;

namespace WiiGC.Core.Services.Wii;

internal static class WiiFileSystem
{
    private const int BootSize = 0x440;
    private const int MaxFstSize = 0x4000000;

    public static WiiPartitionInfo Read(WiiPartitionReader reader)
    {
        byte[] boot = new byte[BootSize];

        reader.Read(0, boot);

        string gameId = Encoding.ASCII.GetString(boot, 0, 6);
        string title = ReadString(boot, 0x20, 0x40);
        long dolOffset = (long)BinaryPrimitives.ReadUInt32BigEndian(boot.AsSpan(0x420)) << 2;
        long fstOffset = (long)BinaryPrimitives.ReadUInt32BigEndian(boot.AsSpan(0x424)) << 2;
        long fstSize = (long)BinaryPrimitives.ReadUInt32BigEndian(boot.AsSpan(0x428)) << 2;

        if (fstSize < 12 || fstSize > MaxFstSize || fstOffset + fstSize > reader.Length)
            throw new InvalidDataException("FST 위치 또는 크기가 올바르지 않습니다.");

        byte[] fst = new byte[fstSize];

        reader.Read(fstOffset, fst);

        uint entryCount = BinaryPrimitives.ReadUInt32BigEndian(fst.AsSpan(8));
        long tableEnd = (long)entryCount * 12;

        if (entryCount < 1 || tableEnd > fstSize)
            throw new InvalidDataException("FST 엔트리 수가 올바르지 않습니다.");

        var files = new List<WiiFileEntry>();
        var dirNames = new List<string>();
        var dirEnds = new List<uint>();

        for (uint i = 1; i < entryCount; i++)
        {
            while (dirEnds.Count > 0 && i >= dirEnds[^1])
            {
                dirEnds.RemoveAt(dirEnds.Count - 1);
                dirNames.RemoveAt(dirNames.Count - 1);
            }

            var entry = fst.AsSpan((int)(i * 12), 12);
            bool isDirectory = entry[0] != 0;
            int nameOffset = (entry[1] << 16) | (entry[2] << 8) | entry[3];
            uint first = BinaryPrimitives.ReadUInt32BigEndian(entry[4..]);
            uint second = BinaryPrimitives.ReadUInt32BigEndian(entry[8..]);
            long nameStart = tableEnd + nameOffset;

            if (nameStart >= fstSize)
                throw new InvalidDataException("FST 이름 오프셋이 올바르지 않습니다.");

            string name = ReadString(fst, (int)nameStart, (int)(fstSize - nameStart));

            if (isDirectory)
            {
                if (second <= i || second > entryCount)
                    throw new InvalidDataException("FST 디렉터리 범위가 올바르지 않습니다.");

                dirNames.Add(name);
                dirEnds.Add(second);

                continue;
            }

            string path = "/" + string.Join('/', dirNames.Append(name));

            files.Add(new WiiFileEntry(path, (long)first << 2, second));
        }

        return new WiiPartitionInfo(gameId, title, dolOffset, fstOffset, fstSize, files);
    }

    private static string ReadString(byte[] buffer, int start, int max)
    {
        int end = Array.IndexOf(buffer, (byte)0, start, max);
        int length = end < 0 ? max : end - start;

        return Encoding.UTF8.GetString(buffer, start, length);
    }
}