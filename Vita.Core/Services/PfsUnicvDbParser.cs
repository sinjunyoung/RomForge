using System.Text;
using Vita.Core.Models;

namespace Vita.Core.Services;

public sealed class PfsUnicvDbParser
{
    private const int PageSize = 0x400;
    private const int MaxSignaturesPerTableIftbl = 0x32;
    private const int MaxSignaturesPerTableIcvdb = 0x2D;

    public static List<PfsUnicvEntry> Parse(string unicvDbPath, int entryCount)
    {
        using var stream = File.OpenRead(unicvDbPath);

        return Parse(stream, entryCount);
    }

    public static List<PfsUnicvEntry> Parse(Stream stream, int entryCount)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        var magic = reader.ReadBytes(8);

        if (Encoding.ASCII.GetString(magic) != "SCEIRODB")
            throw new InvalidDataException("unicv.db magic가 올바르지 않습니다.");

        var entries = new List<PfsUnicvEntry>(entryCount);
        long page = 1;

        for (int i = 0; i < entryCount; i++)
        {
            stream.Seek(page * PageSize, SeekOrigin.Begin);

            var tableMagic = reader.ReadBytes(8);
            string magicStr = Encoding.ASCII.GetString(tableMagic);
            uint nSectors;
            uint fileSectorSize;
            byte[] dbSeed;
            bool hasDbSeed;
            int maxSignaturesPerTable;

            if (magicStr == "SCEIFTBL")
            {
                uint version = reader.ReadUInt32();

                reader.ReadUInt32();
                reader.ReadUInt32();
                nSectors = reader.ReadUInt32();
                fileSectorSize = reader.ReadUInt32();
                reader.ReadUInt32();
                reader.ReadBytes(20);
                dbSeed = reader.ReadBytes(20);
                hasDbSeed = version > 1;
                maxSignaturesPerTable = MaxSignaturesPerTableIftbl;
            }
            else if (magicStr == "SCEICVDB")
            {
                reader.ReadUInt32();
                fileSectorSize = reader.ReadUInt32();
                reader.ReadUInt32();
                reader.ReadUInt32();
                reader.ReadUInt32();
                reader.ReadUInt32();
                reader.ReadUInt64();
                nSectors = reader.ReadUInt32();
                reader.ReadBytes(20);
                dbSeed = [];
                hasDbSeed = false;
                maxSignaturesPerTable = MaxSignaturesPerTableIcvdb;
            }
            else if (magicStr == "SCEINULL")
            {
                reader.ReadUInt32();
                reader.ReadUInt32();
                reader.ReadUInt32();
                reader.ReadUInt32();
                nSectors = 0;
                fileSectorSize = 0;
                dbSeed = [];
                hasDbSeed = false;
                maxSignaturesPerTable = MaxSignaturesPerTableIftbl;
            }
            else
                throw new InvalidDataException($"unicv.db 항목 {i}의 magic이 올바르지 않습니다 (page {page}): {magicStr}");

            entries.Add(new PfsUnicvEntry
            {
                FileSectorSize = fileSectorSize,
                DbSeed = dbSeed,
                NSectors = nSectors,
                HasDbSeed = hasDbSeed,
                TableMagic = magicStr
            });

            int nSigTables = nSectors == 0 ? 0 : (int)((nSectors + maxSignaturesPerTable - 1) / maxSignaturesPerTable);

            page += 1 + nSigTables;
        }

        return entries;
    }
}