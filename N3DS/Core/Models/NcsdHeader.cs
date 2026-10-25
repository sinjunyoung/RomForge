namespace N3DS.Core.Models;

public class NcsdHeader
{
    public ulong MediaId { get; set; }

    public (uint offset, uint size)[] PartitionMap { get; set; } = new (uint, uint)[8];
}