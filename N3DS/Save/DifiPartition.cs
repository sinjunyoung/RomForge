using N3DS.Save.Interfaces;
using N3DS.Save.Models;

namespace N3DS.Save;

public class DifiPartition : IRandomAccessFile
{
    private readonly DualFile _dpfsLevel1;
    private readonly DpfsLevel _dpfsLevel2;
    private readonly DpfsLevel _dpfsLevel3;
    private readonly IvfcLevel _ivfcLevel1;
    private readonly IvfcLevel _ivfcLevel2;
    private readonly IvfcLevel _ivfcLevel3;
    private readonly IvfcLevel _ivfcLevel4;

    public int Length => _ivfcLevel4.Length;

    private static uint Ilog(int blockLen) => (uint)(63 - System.Numerics.BitOperations.LeadingZeroCount((ulong)blockLen));

    private static int IvfcAlign(int offset, int len, int blockLen) => len >= 4 * blockLen ? Misc.AlignUp(offset, blockLen) : Misc.AlignUp(offset, 8);

    private static DifiPartitionInfo CalculateInfo(DifiPartitionParam p)
    {
        int ivfcLevel4Len = p.DataLen;
        int ivfcLevel3Len = Misc.DivideUp(ivfcLevel4Len, p.IvfcLevel4BlockLen) * 0x20;
        int ivfcLevel2Len = Misc.DivideUp(ivfcLevel3Len, p.IvfcLevel3BlockLen) * 0x20;
        int ivfcLevel1Len = Misc.DivideUp(ivfcLevel2Len, p.IvfcLevel2BlockLen) * 0x20;
        int masterHashLen = Misc.DivideUp(ivfcLevel1Len, p.IvfcLevel1BlockLen) * 0x20;
        int ivfcLevel1Offset = 0;
        int ivfcLevel2Offset = IvfcAlign(ivfcLevel1Offset + ivfcLevel1Len, ivfcLevel2Len, p.IvfcLevel2BlockLen);
        int ivfcLevel3Offset = IvfcAlign(ivfcLevel2Offset + ivfcLevel2Len, ivfcLevel3Len, p.IvfcLevel3BlockLen);
        int ivfcLevel4Offset = IvfcAlign(ivfcLevel3Offset + ivfcLevel3Len, ivfcLevel4Len, p.IvfcLevel4BlockLen);
        int ivfcEnd = ivfcLevel4Offset + ivfcLevel4Len;
        int duplicateDataLen = p.ExternalIvfcLevel4 ? ivfcLevel4Offset : ivfcEnd;
        int dpfsLevel3Len = Misc.AlignUp(duplicateDataLen, p.DpfsLevel3BlockLen);
        int dpfsLevel2Len = Misc.AlignUp((1 + (dpfsLevel3Len / p.DpfsLevel3BlockLen - 1) / 32) * 4, p.DpfsLevel2BlockLen);
        int dpfsLevel1Len = (1 + (dpfsLevel2Len / p.DpfsLevel2BlockLen - 1) / 32) * 4;
        int dpfsLevel1Offset = 0;
        int dpfsLevel2Offset = dpfsLevel1Offset + dpfsLevel1Len * 2;
        int dpfsLevel3Offset = Misc.AlignUp(dpfsLevel2Offset + dpfsLevel2Len * 2, p.DpfsLevel3BlockLen);
        int dpfsEnd = dpfsLevel3Offset + dpfsLevel3Len * 2;
        int partitionLen;
        int externalIvfcLevel4Offset;

        if (p.ExternalIvfcLevel4)
        {
            externalIvfcLevel4Offset = Misc.AlignUp(dpfsEnd, p.IvfcLevel4BlockLen);
            partitionLen = externalIvfcLevel4Offset + ivfcLevel4Len;
        }
        else
        {
            externalIvfcLevel4Offset = 0;
            partitionLen = dpfsEnd;
        }

        int ivfcDescriptorOffset = DifiHeader.Size;
        int dpfsDescriptorOffset = ivfcDescriptorOffset + IvfcDescriptor.Size;
        int masterHashOffset = dpfsDescriptorOffset + DpfsDescriptor.Size;
        int descriptorLen = masterHashOffset + masterHashLen;
        var difiHeader = new DifiHeader
        {
            Magic = "DIFI"u8.ToArray(),
            Version = 0x10000,
            IvfcDescriptorOffset = (ulong)ivfcDescriptorOffset,
            IvfcDescriptorSize = (ulong)IvfcDescriptor.Size,
            DpfsDescriptorOffset = (ulong)dpfsDescriptorOffset,
            DpfsDescriptorSize = (ulong)DpfsDescriptor.Size,
            PartitionHashOffset = (ulong)masterHashOffset,
            PartitionHashSize = (ulong)masterHashLen,
            ExternalIvfcLevel4 = (byte)(p.ExternalIvfcLevel4 ? 1 : 0),
            DpfsSelector = 0,
            Padding = 0,
            IvfcLevel4Offset = (ulong)externalIvfcLevel4Offset,
        };
        var ivfcDescriptor = new IvfcDescriptor
        {
            Magic = "IVFC"u8.ToArray(),
            Version = 0x20000,
            MasterHashSize = (ulong)masterHashLen,
            Level1Offset = (ulong)ivfcLevel1Offset,
            Level1Size = (ulong)ivfcLevel1Len,
            Level1BlockLog = Ilog(p.IvfcLevel1BlockLen),
            Padding1 = 0,
            Level2Offset = (ulong)ivfcLevel2Offset,
            Level2Size = (ulong)ivfcLevel2Len,
            Level2BlockLog = Ilog(p.IvfcLevel2BlockLen),
            Padding2 = 0,
            Level3Offset = (ulong)ivfcLevel3Offset,
            Level3Size = (ulong)ivfcLevel3Len,
            Level3BlockLog = Ilog(p.IvfcLevel3BlockLen),
            Padding3 = 0,
            Level4Offset = (ulong)ivfcLevel4Offset,
            Level4Size = (ulong)ivfcLevel4Len,
            Level4BlockLog = Ilog(p.IvfcLevel4BlockLen),
            Padding4 = 0,
            IvfcDescriptorSize = (ulong)IvfcDescriptor.Size,
        };
        var dpfsDescriptor = new DpfsDescriptor
        {
            Magic = "DPFS"u8.ToArray(),
            Version = 0x10000,
            Level1Offset = (ulong)dpfsLevel1Offset,
            Level1Size = (ulong)dpfsLevel1Len,
            Level1BlockLog = 0,
            Padding1 = 0,
            Level2Offset = (ulong)dpfsLevel2Offset,
            Level2Size = (ulong)dpfsLevel2Len,
            Level2BlockLog = Ilog(p.DpfsLevel2BlockLen),
            Padding2 = 0,
            Level3Offset = (ulong)dpfsLevel3Offset,
            Level3Size = (ulong)dpfsLevel3Len,
            Level3BlockLog = Ilog(p.DpfsLevel3BlockLen),
            Padding3 = 0,
        };

        return new DifiPartitionInfo
        {
            DifiHeader = difiHeader,
            IvfcDescriptor = ivfcDescriptor,
            DpfsDescriptor = dpfsDescriptor,
            DescriptorLen = descriptorLen,
            PartitionLen = partitionLen,
        };
    }

    public static (int descriptorLen, int partitionLen) CalculateSize(DifiPartitionParam param)
    {
        var info = CalculateInfo(param);
        return (info.DescriptorLen, info.PartitionLen);
    }

    public static void Format(IRandomAccessFile descriptor, DifiPartitionParam param)
    {
        var info = CalculateInfo(param);

        info.DifiHeader.Write(descriptor, 0);
        info.IvfcDescriptor.Write(descriptor, (int)info.DifiHeader.IvfcDescriptorOffset);
        info.DpfsDescriptor.Write(descriptor, (int)info.DifiHeader.DpfsDescriptorOffset);
    }

    public DifiPartition(IRandomAccessFile descriptor, IRandomAccessFile partition)
    {
        var header = DifiHeader.Read(descriptor, 0);

        if (!header.Magic.AsSpan().SequenceEqual("DIFI"u8) || header.Version != 0x10000)
            throw new InvalidDataException("DifiPartition: unexpected DIFI magic/version");

        if (header.IvfcDescriptorSize != IvfcDescriptor.Size)
            throw new InvalidDataException("DifiPartition: unexpected ivfc_descriptor_size");

        var ivfc = IvfcDescriptor.Read(descriptor, (int)header.IvfcDescriptorOffset);

        if (!ivfc.Magic.AsSpan().SequenceEqual("IVFC"u8) || ivfc.Version != 0x20000)
            throw new InvalidDataException("DifiPartition: unexpected IVFC magic/version");

        if (header.PartitionHashSize != ivfc.MasterHashSize)
            throw new InvalidDataException("DifiPartition: partition_hash_size mismatch");

        if (header.DpfsDescriptorSize != DpfsDescriptor.Size)
            throw new InvalidDataException("DifiPartition: unexpected dpfs_descriptor_size");

        var dpfs = DpfsDescriptor.Read(descriptor, (int)header.DpfsDescriptorOffset);

        if (!dpfs.Magic.AsSpan().SequenceEqual("DPFS"u8) || dpfs.Version != 0x10000)
            throw new InvalidDataException("DifiPartition: unexpected DPFS magic/version");

        var dpfsLevel0 = new SubFile(descriptor, 0x39, 1);
        var dpfsLevel1Pair = new IRandomAccessFile[]
        {
            new SubFile(partition, (int)dpfs.Level1Offset, (int)dpfs.Level1Size),
            new SubFile(partition, (int)(dpfs.Level1Offset + dpfs.Level1Size), (int)dpfs.Level1Size),
        };
        var dpfsLevel2Pair = new IRandomAccessFile[]
        {
            new SubFile(partition, (int)dpfs.Level2Offset, (int)dpfs.Level2Size),
            new SubFile(partition, (int)(dpfs.Level2Offset + dpfs.Level2Size), (int)dpfs.Level2Size),
        };
        var dpfsLevel3Pair = new IRandomAccessFile[]
        {
            new SubFile(partition, (int)dpfs.Level3Offset, (int)dpfs.Level3Size),
            new SubFile(partition, (int)(dpfs.Level3Offset + dpfs.Level3Size), (int)dpfs.Level3Size),
        };

        _dpfsLevel1 = new DualFile(dpfsLevel0, dpfsLevel1Pair);
        _dpfsLevel2 = new DpfsLevel(_dpfsLevel1, dpfsLevel2Pair, 1 << (int)dpfs.Level2BlockLog);
        _dpfsLevel3 = new DpfsLevel(_dpfsLevel2, dpfsLevel3Pair, 1 << (int)dpfs.Level3BlockLog);

        var ivfcLevel0 = new SubFile(descriptor, (int)header.PartitionHashOffset, (int)header.PartitionHashSize);

        _ivfcLevel1 = new IvfcLevel(ivfcLevel0, new SubFile(_dpfsLevel3, (int)ivfc.Level1Offset, (int)ivfc.Level1Size), 1 << (int)ivfc.Level1BlockLog);
        _ivfcLevel2 = new IvfcLevel(_ivfcLevel1, new SubFile(_dpfsLevel3, (int)ivfc.Level2Offset, (int)ivfc.Level2Size), 1 << (int)ivfc.Level2BlockLog);
        _ivfcLevel3 = new IvfcLevel(_ivfcLevel2, new SubFile(_dpfsLevel3, (int)ivfc.Level3Offset, (int)ivfc.Level3Size), 1 << (int)ivfc.Level3BlockLog);

        IRandomAccessFile ivfcLevel4Data = header.ExternalIvfcLevel4 == 0 ? new SubFile(_dpfsLevel3, (int)ivfc.Level4Offset, (int)ivfc.Level4Size) : new SubFile(partition, (int)header.IvfcLevel4Offset, (int)ivfc.Level4Size);

        _ivfcLevel4 = new IvfcLevel(_ivfcLevel3, ivfcLevel4Data, 1 << (int)ivfc.Level4BlockLog);
    }

    public void Read(int pos, byte[] buf, int offset, int count) => _ivfcLevel4.Read(pos, buf, offset, count);

    public void Write(int pos, byte[] buf, int offset, int count) => _ivfcLevel4.Write(pos, buf, offset, count);

    public void Commit()
    {
        _ivfcLevel4.Commit();
        _ivfcLevel3.Commit();
        _ivfcLevel2.Commit();
        _ivfcLevel1.Commit();
        _dpfsLevel3.Commit();
        _dpfsLevel2.Commit();
        _dpfsLevel1.Commit();
    }
}