using System.Text;
using Vita.Core.Models;

namespace Vita.Core.Services;

public sealed class PfsUnicvDbParser
{
    private const int SigTablePageSize = 0x400;
    private const int MaxSignaturesPerTable = 0x32;

    public static List<PfsUnicvEntry> Parse(string unicvDbPath, int entryCount)
    {
        using var stream = File.OpenRead(unicvDbPath);
        using var reader = new BinaryReader(stream);

        var magic = reader.ReadBytes(8);

        if (Encoding.ASCII.GetString(magic) != "SCEIRODB")
            throw new InvalidDataException("unicv.db magic가 올바르지 않습니다.");

        reader.ReadBytes(24);

        var entries = new List<PfsUnicvEntry>(entryCount);

        for (int i = 0; i < entryCount; i++)
        {
            var tableMagic = reader.ReadBytes(8);

            if (Encoding.ASCII.GetString(tableMagic) != "SCEIFTBL")
                throw new InvalidDataException($"unicv.db 항목 {i}의 magic이 올바르지 않습니다.");

            reader.ReadUInt32();
            reader.ReadUInt32();
            reader.ReadUInt32();

            uint nSectors = reader.ReadUInt32();
            uint fileSectorSize = reader.ReadUInt32();

            reader.ReadUInt32();
            reader.ReadBytes(20);

            byte[] dbSeed = reader.ReadBytes(20);

            entries.Add(new PfsUnicvEntry
            {
                FileSectorSize = fileSectorSize,
                DbSeed = dbSeed,
                NSectors = nSectors
            });

            if (nSectors > 0)
            {
                int nSigTables = (int)((nSectors + MaxSignaturesPerTable - 1) / MaxSignaturesPerTable);

                stream.Seek((long)nSigTables * SigTablePageSize, SeekOrigin.Current);
            }
        }

        return entries;
    }
}