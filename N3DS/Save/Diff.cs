using N3DS.Save.Interfaces;
using N3DS.Save.Models;

namespace N3DS.Save;

public class Diff
{
    private readonly int _parentLen;
    private readonly IRandomAccessFile _headerFile;
    private readonly DualFile _tableUpper;
    private readonly IvfcLevel _tableLower;
    private readonly DifiPartition _partition;
    private readonly ulong _uniqueId;

    public int ParentLen => _parentLen;

    public ulong UniqueId => _uniqueId;

    public DifiPartition Partition => _partition;

    private static DiffInfo CalculateInfo(DifiPartitionParam param)
    {
        var (descriptorLen, partitionLen) = DifiPartition.CalculateSize(param);
        int partitionAlign = param.GetAlign();
        int secondaryTableOffset = 0x200;
        int tableLen = descriptorLen;
        int primaryTableOffset = Misc.AlignUp(secondaryTableOffset + tableLen, 8);
        int partitionOffset = Misc.AlignUp(primaryTableOffset + tableLen, partitionAlign);
        int end = partitionOffset + partitionLen;

        return new DiffInfo
        {
            SecondaryTableOffset = secondaryTableOffset,
            PrimaryTableOffset = primaryTableOffset,
            TableLen = tableLen,
            PartitionOffset = partitionOffset,
            PartitionLen = partitionLen,
            End = end,
        };
    }

    public static int CalculateSize(DifiPartitionParam param) => CalculateInfo(param).End;

    public static void Format(IRandomAccessFile file, (ISigner signer, byte[] key)? signer, DifiPartitionParam param, ulong uniqueId)
    {
        var zeros = new byte[0x200];

        file.Write(0, zeros, 0, 0x200);

        IRandomAccessFile headerFileBare = new SubFile(file, 0x100, 0x100);
        IRandomAccessFile headerFile = signer is var (s, k) ? SignedFile.New(new SubFile(file, 0, 0x10), headerFileBare, s, k) : headerFileBare;

        var info = CalculateInfo(param);
        var header = new DiffHeader
        {
            Magic = "DIFF"u8.ToArray(),
            Version = 0x30000,
            SecondaryTableOffset = (ulong)info.SecondaryTableOffset,
            PrimaryTableOffset = (ulong)info.PrimaryTableOffset,
            TableSize = (ulong)info.TableLen,
            PartitionOffset = (ulong)info.PartitionOffset,
            PartitionSize = (ulong)info.PartitionLen,
            ActiveTable = 1,
            Padding = new byte[3],
            Sha = new byte[0x20],
            UniqueId = uniqueId,
        };

        header.Write(headerFile, 0);

        var table = new IvfcLevel(new SubFile(headerFile, 0x34, 0x20), new SubFile(file, info.SecondaryTableOffset, info.TableLen), info.TableLen);

        DifiPartition.Format(table, param);

        table.Commit();
        headerFile.Commit();
    }

    public Diff(IRandomAccessFile file, (ISigner signer, byte[] key)? signer)
    {
        var check = new byte[16];

        file.Read(0x100, check, 0, 16);

        _parentLen = file.Length;

        IRandomAccessFile headerFileBare = new SubFile(file, 0x100, 0x100);
        IRandomAccessFile headerFile = signer is var (s, k) ? SignedFile.New(new SubFile(file, 0, 0x10), headerFileBare, s, k) : headerFileBare;
        var header = DiffHeader.Read(headerFile, 0);

        if (!header.Magic.AsSpan().SequenceEqual("DIFF"u8) || header.Version != 0x30000)
            throw new InvalidDataException("Diff: unexpected DIFF magic/version");

        var tableSelector = new SubFile(headerFile, 0x30, 1);
        var tableHash = new SubFile(headerFile, 0x34, 0x20);
        var tablePair = new IRandomAccessFile[]
        {
            new SubFile(file, (int)header.PrimaryTableOffset,   (int)header.TableSize),
            new SubFile(file, (int)header.SecondaryTableOffset, (int)header.TableSize),
        };

        _tableUpper = new DualFile(tableSelector, tablePair);
        _tableLower = new IvfcLevel(tableHash, _tableUpper, (int)header.TableSize);

        var partitionSubFile = new SubFile(file, (int)header.PartitionOffset, (int)header.PartitionSize);

        _partition = new DifiPartition(_tableLower, partitionSubFile);
        _headerFile = headerFile;
        _uniqueId = header.UniqueId;
    }

    public void Commit()
    {
        _partition.Commit();
        _tableLower.Commit();
        _tableUpper.Commit();
        _headerFile.Commit();
    }
}