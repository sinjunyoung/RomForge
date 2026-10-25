using N3DS.Save.Interfaces;
using N3DS.Save.Models;

namespace N3DS.Save;

public class Disa
{
    private readonly IRandomAccessFile _headerFile;
    private readonly DualFile _tableUpper;
    private readonly IvfcLevel _tableLower;
    private readonly List<DifiPartition> _partitions;

    public int PartitionCount => _partitions.Count;

    public DifiPartition this[int index] => _partitions[index];

    private static DisaInfo CalculateInfo(DifiPartitionParam paramA, DifiPartitionParam? paramB)
    {
        var (descriptorALen, partitionALen) = DifiPartition.CalculateSize(paramA);
        var (descriptorBLen, partitionBLen) = paramB != null ? DifiPartition.CalculateSize(paramB) : (0, 0);
        int descriptorAOffset = 0;
        int descriptorBOffset, tableLen;

        if (paramB != null)
        {
            descriptorBOffset = Misc.AlignUp(descriptorAOffset + descriptorALen, 8);
            tableLen = Misc.AlignUp(descriptorBOffset + descriptorBLen, 8);
        }
        else
        {
            descriptorBOffset = 0;
            tableLen = descriptorALen;
        }

        int secondaryTableOffset = 0x200;
        int primaryTableOffset = Misc.AlignUp(secondaryTableOffset + tableLen, 8);
        int partitionAAlign = paramA.GetAlign();
        int partitionAOffset = Misc.AlignUp(primaryTableOffset + tableLen, partitionAAlign);
        int partitionBOffset, end;

        if (paramB != null)
        {
            int partitionBAlign = paramB.GetAlign();

            partitionBOffset = Misc.AlignUp(partitionAOffset + partitionALen, partitionBAlign);
            end = partitionBOffset + partitionBLen;
        }
        else
        {
            partitionBOffset = 0;
            end = partitionAOffset + partitionALen;
        }

        return new DisaInfo
        {
            SecondaryTableOffset = secondaryTableOffset,
            PrimaryTableOffset = primaryTableOffset,
            TableLen = tableLen,
            DescriptorAOffset = descriptorAOffset,
            DescriptorALen = descriptorALen,
            PartitionAOffset = partitionAOffset,
            PartitionALen = partitionALen,
            DescriptorBOffset = descriptorBOffset,
            DescriptorBLen = descriptorBLen,
            PartitionBOffset = partitionBOffset,
            PartitionBLen = partitionBLen,
            End = end,
        };
    }

    public static int CalculateSize(DifiPartitionParam paramA, DifiPartitionParam? paramB) => CalculateInfo(paramA, paramB).End;

    public static void Format(IRandomAccessFile file, (ISigner signer, byte[] key)? signer, DifiPartitionParam paramA, DifiPartitionParam? paramB)
    {
        var zeros = new byte[0x200];

        file.Write(0, zeros, 0, 0x200);

        IRandomAccessFile headerFileBare = new SubFile(file, 0x100, 0x100);
        IRandomAccessFile headerFile = signer is var (s, k) ? SignedFile.NewUnverified(new SubFile(file, 0, 0x10), headerFileBare, s, k) : headerFileBare;
        var info = CalculateInfo(paramA, paramB);
        var header = new DisaHeader
        {
            Magic = "DISA"u8.ToArray(),
            Version = 0x40000,
            PartitionCount = (uint)(paramB != null ? 2 : 1),
            Padding1 = 0,
            SecondaryTableOffset = (ulong)info.SecondaryTableOffset,
            PrimaryTableOffset = (ulong)info.PrimaryTableOffset,
            TableSize = (ulong)info.TableLen,
            PartitionDescriptor =
            [
                ((ulong)info.DescriptorAOffset, (ulong)info.DescriptorALen),
                ((ulong)info.DescriptorBOffset, (ulong)info.DescriptorBLen),
            ],
            Partition =
            [
                ((ulong)info.PartitionAOffset, (ulong)info.PartitionALen),
                ((ulong)info.PartitionBOffset, (ulong)info.PartitionBLen),
            ],
            ActiveTable = 1,
        };

        header.Write(headerFile, 0);

        var table = new IvfcLevel(new SubFile(headerFile, 0x6C, 0x20), new SubFile(file, info.SecondaryTableOffset, info.TableLen), info.TableLen);

        DifiPartition.Format(new SubFile(table, info.DescriptorAOffset, info.DescriptorALen), paramA);

        if (paramB != null)
            DifiPartition.Format(new SubFile(table, info.DescriptorBOffset, info.DescriptorBLen), paramB);

        table.Commit();
        headerFile.Commit();
    }

    public Disa(IRandomAccessFile file, (ISigner signer, byte[] key)? signer)
    {
        IRandomAccessFile headerFileBare = new SubFile(file, 0x100, 0x100);
        IRandomAccessFile headerFile = signer is var (s, k) ? SignedFile.New(new SubFile(file, 0, 0x10), headerFileBare, s, k) : headerFileBare;
        var header = DisaHeader.Read(headerFile, 0);

        if (!header.Magic.AsSpan().SequenceEqual("DISA"u8) || header.Version != 0x40000)
            throw new InvalidDataException("Disa: unexpected DISA magic/version");

        if (header.PartitionCount != 1 && header.PartitionCount != 2)
            throw new InvalidDataException($"Disa: unexpected partition_count {header.PartitionCount}");

        var tableSelector = new SubFile(headerFile, 0x68, 1);
        var tableHash = new SubFile(headerFile, 0x6C, 0x20);
        var tablePair = new IRandomAccessFile[]
        {
            new SubFile(file, (int)header.PrimaryTableOffset,   (int)header.TableSize),
            new SubFile(file, (int)header.SecondaryTableOffset, (int)header.TableSize),
        };

        _tableUpper = new DualFile(tableSelector, tablePair);
        _tableLower = new IvfcLevel(tableHash, _tableUpper, (int)header.TableSize);
        _partitions = new List<DifiPartition>((int)header.PartitionCount);

        for (int i = 0; i < (int)header.PartitionCount; i++)
        {
            var (dOffset, dSize) = header.PartitionDescriptor[i];
            var (pOffset, pSize) = header.Partition[i];
            var descriptor = new SubFile(_tableLower, (int)dOffset, (int)dSize);
            var partition = new SubFile(file, (int)pOffset, (int)pSize);

            _partitions.Add(new DifiPartition(descriptor, partition));
        }

        _headerFile = headerFile;
    }

    public void Commit()
    {
        foreach (var partition in _partitions)
            partition.Commit();

        _tableLower.Commit();
        _tableUpper.Commit();
        _headerFile.Commit();
    }
}