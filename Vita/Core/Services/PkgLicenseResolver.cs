using System.Text;
using Vita.Core.Cryptography;
using Vita.Core.Models;

namespace Vita.Core.Services;

public static class PkgLicenseResolver
{
    public static byte[] ResolveKlicensee(string license, string expectedContentId)
    {
        if (File.Exists(license))
        {
            var parsed = WorkBinReader.Read(license);

            if (!parsed.ContentId.Equals(expectedContentId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"라이선스 파일의 content_id({parsed.ContentId})가 PKG의 content_id({expectedContentId})와 일치하지 않습니다.");

            return parsed.Klicensee;
        }

        byte[] rif = ZrifDecoder.Decode(license);
        string rifContentId = Encoding.ASCII.GetString(rif, WorkBinLicense.ContentIdOffset, WorkBinLicense.ContentIdSize).TrimEnd('\0');

        if (!rifContentId.Equals(expectedContentId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"zRIF의 content_id({rifContentId})가 PKG의 content_id({expectedContentId})와 일치하지 않습니다.");

        return rif.AsSpan(WorkBinLicense.KlicenseeOffset, WorkBinLicense.KlicenseeSize).ToArray();
    }

    public static byte[] BuildWorkBin(string contentId, byte[] klicensee)
    {
        var buf = new byte[WorkBinLicense.WriteSize];
        var contentIdBytes = Encoding.ASCII.GetBytes(contentId);

        WorkBinLicense.FixedHeader.CopyTo(buf, 0);
        contentIdBytes.CopyTo(buf, WorkBinLicense.ContentIdOffset);
        klicensee.CopyTo(buf, WorkBinLicense.KlicenseeOffset);

        return buf;
    }
}