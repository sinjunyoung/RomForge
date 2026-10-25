namespace N3DS.Save.Models;

public struct DisaInfo
{
    public int SecondaryTableOffset;

    public int PrimaryTableOffset;

    public int TableLen;

    public int DescriptorAOffset;

    public int DescriptorALen;

    public int PartitionAOffset;

    public int PartitionALen;

    public int DescriptorBOffset;

    public int DescriptorBLen;

    public int PartitionBOffset;

    public int PartitionBLen;

    public int End;
}