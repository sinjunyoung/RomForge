using System.Text;

namespace N3DS.Core.FileSystem;

public static class SmdhWriter
{
    private const uint SmdhMagic = 0x48444D53;
    private const int TitleStructSize = 0x200;
    private const int ShortDescMaxBytes = 0x80;
    private const int LongDescMaxBytes = 0x100;
    private const int PublisherMaxBytes = 0x80;

    public static byte[]? ApplyOverride(byte[] smdhData, string? shortDescription, string? publisher)
    {
        if (string.IsNullOrEmpty(shortDescription) && string.IsNullOrEmpty(publisher))
            return null;

        if (smdhData.Length < 0x36C0)
            return null;

        uint magic = BitConverter.ToUInt32(smdhData, 0);

        if (magic != SmdhMagic)
            return null;

        byte[] result = (byte[])smdhData.Clone();
        int targetSlot = FindRepresentativeSlot(result);

        if (targetSlot < 0)
            return null;

        int offset = 0x08 + targetSlot * TitleStructSize;

        if (!string.IsNullOrEmpty(shortDescription))
        {
            WriteUtf16(result, offset + 0x000, ShortDescMaxBytes, shortDescription);
            WriteUtf16(result, offset + 0x080, LongDescMaxBytes, shortDescription);
        }

        if (!string.IsNullOrEmpty(publisher))
            WriteUtf16(result, offset + 0x180, PublisherMaxBytes, publisher);

        return result;
    }

    private static int FindRepresentativeSlot(byte[] data)
    {
        const int english = 1;
        const int japanese = 0;

        if (HasContent(data, 0x08 + english * TitleStructSize, ShortDescMaxBytes))
            return english;

        if (HasContent(data, 0x08 + japanese * TitleStructSize, ShortDescMaxBytes))
            return japanese;

        for (int i = 0; i < 16; i++)
        {
            if (HasContent(data, 0x08 + i * TitleStructSize, ShortDescMaxBytes))
                return i;
        }

        return -1;
    }

    private static bool HasContent(byte[] data, int offset, int maxBytes)
    {
        int limit = Math.Min(offset + maxBytes, data.Length);

        for (int i = offset; i < limit - 1; i += 2)
        {
            if (data[i] != 0 || data[i + 1] != 0)
                return true;
        }

        return false;
    }

    private static void WriteUtf16(byte[] data, int offset, int maxBytes, string text)
    {
        Array.Clear(data, offset, maxBytes);

        int maxChars = maxBytes / 2 - 1;
        string trimmed = text.Length > maxChars ? text[..maxChars] : text;
        byte[] encoded = Encoding.Unicode.GetBytes(trimmed);

        Array.Copy(encoded, 0, data, offset, Math.Min(encoded.Length, maxBytes - 2));
    }
}