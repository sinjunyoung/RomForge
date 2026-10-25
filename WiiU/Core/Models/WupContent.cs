namespace WiiU.Core.Models;

public sealed record WupContent(uint ContentId, ushort Index, ushort Type, ulong Size, byte[] Hash)
{
    public bool IsHashed => (Type & 0x0002) != 0;

    public string CIDHex => ContentId.ToString("x8");
}