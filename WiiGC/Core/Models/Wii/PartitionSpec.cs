namespace WiiGC.Core.Models.Wii;

internal sealed record PartitionSpec(long ContainerOffset, long DataStart, long DataSize, byte[] Key);