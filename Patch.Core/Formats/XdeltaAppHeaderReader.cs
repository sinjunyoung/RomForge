using System.Text;
using System.Text.RegularExpressions;

namespace Patch.Core.Formats;

public static class XdeltaAppHeaderReader
{
    private static readonly Regex CompressedExtensionRegex = new(@"\.(chd|rvz)(\W|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string? ReadSourceFileNameHint(string patchPath)
    {
        try
        {
            using var fs = new FileStream(patchPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);

            if (fs.Length < 5)
                return null;

            var magic = br.ReadBytes(4);

            if (magic[0] != 0xD6 || magic[1] != 0xC3 || magic[2] != 0xC4)
                return null;

            byte hdrIndicator = br.ReadByte();
            bool hasDecompress = (hdrIndicator & 0x01) != 0;
            bool hasCodeTable = (hdrIndicator & 0x02) != 0;
            bool hasAppHeader = (hdrIndicator & 0x04) != 0;

            if (hasDecompress)
                br.ReadByte();

            if (hasCodeTable)
            {
                int codeTableLen = ReadVcdiffInt(br);
                br.ReadBytes(codeTableLen);
            }

            if (!hasAppHeader)
                return null;

            int appHeaderLen = ReadVcdiffInt(br);

            if (appHeaderLen <= 0 || appHeaderLen > fs.Length - fs.Position)
                return null;

            return Encoding.Latin1.GetString(br.ReadBytes(appHeaderLen));
        }
        catch
        {
            return null;
        }
    }

    public static bool TargetsCompressedContainer(string patchPath)
    {
        string? hint = ReadSourceFileNameHint(patchPath);

        return hint is not null && CompressedExtensionRegex.IsMatch(hint);
    }

    private static int ReadVcdiffInt(BinaryReader br)
    {
        int value = 0;
        byte b;

        do
        {
            b = br.ReadByte();
            value = (value << 7) | (b & 0x7F);
        } while ((b & 0x80) != 0);

        return value;
    }
}