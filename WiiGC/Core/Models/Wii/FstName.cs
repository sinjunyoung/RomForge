using System.Buffers;
using System.Text;

namespace WiiGC.Core.Models.Wii;

internal static class FstName
{
    private const int EscapeBase = 0xF700;
    private const int EscapeFirst = 0xF780;
    private const int EscapeLast = 0xF7FF;

    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        var builder = new StringBuilder(bytes.Length);

        while (!bytes.IsEmpty)
        {
            var status = Rune.DecodeFromUtf8(bytes, out var rune, out int consumed);

            if (status == OperationStatus.Done && (rune.Value < EscapeFirst || rune.Value > EscapeLast))
            {
                builder.Append(rune.ToString());

                bytes = bytes[consumed..];
                continue;
            }

            builder.Append((char)(EscapeBase + bytes[0]));

            bytes = bytes[1..];
        }

        return builder.ToString();
    }

    public static byte[] Encode(string name)
    {
        var result = new List<byte>(name.Length * 3);
        Span<byte> buffer = stackalloc byte[4];

        foreach (var rune in name.EnumerateRunes())
        {
            if (rune.Value >= EscapeFirst && rune.Value <= EscapeLast)
            {
                result.Add((byte)(rune.Value - EscapeBase));
                continue;
            }

            int length = rune.EncodeToUtf8(buffer);

            for (int i = 0; i < length; i++)
                result.Add(buffer[i]);
        }

        return [.. result];
    }
}