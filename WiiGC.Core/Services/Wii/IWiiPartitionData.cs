namespace WiiGC.Core.Services.Wii;

internal interface IWiiPartitionData
{
    long Length { get; }

    void Read(long offset, Span<byte> destination);
}