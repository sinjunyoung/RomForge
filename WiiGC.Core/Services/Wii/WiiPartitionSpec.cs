namespace WiiGC.Core.Services.Wii;

internal sealed record WiiPartitionSpec(long ContainerOffset, long DataStart, long DataSize, byte[] Key);