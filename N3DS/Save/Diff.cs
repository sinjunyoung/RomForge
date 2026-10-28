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

    public DifiPartition Partition => _partition;

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