namespace N3DS.Core.Crypto;

public class KeySlot
{
    private static readonly byte[] GeneratorConstant = HexToBytes("1ff9e9aac5fe0408024591dc5d52768a");

    public byte[]? KeyX { get; private set; }

    public byte[]? KeyY { get; private set; }

    public byte[]? NormalKey { get; private set; }

    public void SetKeyX(byte[] key) { KeyX = key; TryGenerateNormalKey(); }

    public void SetKeyY(byte[] key) { KeyY = key; TryGenerateNormalKey(); }

    public void SetNormalKey(byte[] key) { NormalKey = key; }

    private void TryGenerateNormalKey()
    {
        if (KeyX == null || KeyY == null)
            return;

        NormalKey = ComputeNormalKey(KeyX, KeyY);
    }

    public static byte[] ComputeNormalKey(byte[] keyX, byte[] keyY)
    {
        var step1 = Lrot128(keyX, 2);
        var step2 = Xor128(step1, keyY);
        var step3 = Add128(step2, GeneratorConstant);

        return Lrot128(step3, 87);
    }

    public static byte[] Lrot128(byte[] input, int rot)
    {
        rot %= 128;
        byte[] result = new byte[16];
        int byteShift = rot / 8;
        int bitShift = rot % 8;

        for (int i = 0; i < 16; i++)
        {
            int srcIdx = (i + byteShift) % 16;
            int nextIdx = (srcIdx + 1) % 16;

            result[i] = bitShift == 0 ? input[srcIdx] : (byte)((input[srcIdx] << bitShift) | (input[nextIdx] >> (8 - bitShift)));
        }

        return result;
    }

    public static byte[] Xor128(byte[] a, byte[] b)
    {
        byte[] result = new byte[16];

        for (int i = 0; i < 16; i++)
            result[i] = (byte)(a[i] ^ b[i]);

        return result;
    }

    public static byte[] Add128(byte[] a, byte[] b)
    {
        byte[] result = new byte[16];
        int carry = 0;

        for (int i = 15; i >= 0; i--)
        {
            int sum = a[i] + b[i] + carry;
            result[i] = (byte)(sum & 0xFF);
            carry = sum >> 8;
        }

        return result;
    }

    public static byte[] HexToBytes(string hex)
    {
        byte[] result = new byte[hex.Length / 2];

        for (int i = 0; i < result.Length; i++)
            result[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);

        return result;
    }
}