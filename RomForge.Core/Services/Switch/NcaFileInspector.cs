using System.IO;
using System.Security.Cryptography;
using Path = System.IO.Path;

namespace RomForge.Core.Services.Switch;

internal static class NcaFileInspector
{
    public static BuiltNca Inspect(string path)
    {
        byte[] hash;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan))
            hash = SHA256.HashData(stream);
                string idHex = Convert.ToHexString(hash, 0, 16).ToLowerInvariant();
        string fileName = Path.GetFileName(path);

        if (!fileName.StartsWith(idHex, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"생성된 NCA의 이름과 해시가 일치하지 않습니다: {fileName}");

        return new BuiltNca(path, fileName, hash[..0x10], hash, new FileInfo(path).Length);
    }
}