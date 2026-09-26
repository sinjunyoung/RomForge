namespace DolphinTool.Core.Rvz;

internal static class RvzMagic
{
    public static bool IsRvz(ReadOnlySpan<byte> header) => header[0] == (byte)'R' && header[1] == (byte)'V' && header[2] == (byte)'Z' && header[3] == 1;

    public static bool IsWia(ReadOnlySpan<byte> header) => header[0] == (byte)'W' && header[1] == (byte)'I' && header[2] == (byte)'A' && header[3] == 1;
}