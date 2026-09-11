using Vita.Core.Cryptography;
using Vita.Core.Models;

namespace Vita.Core.Services;

public sealed class VitaNoNpDrmDecryptor
{
    public List<string> UnsupportedEntries { get; } = [];

    public void Decrypt(string titleIdPath, string destPath, byte[] klicensee, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        string filesDbPath = Path.Combine(titleIdPath, "sce_pfs", "files.db");
        string unicvDbPath = Path.Combine(titleIdPath, "sce_pfs", "unicv.db");

        if (!File.Exists(filesDbPath))
            throw new FileNotFoundException("files.db를 찾을 수 없습니다.", filesDbPath);

        if (!File.Exists(unicvDbPath))
            throw new FileNotFoundException("unicv.db를 찾을 수 없습니다.", unicvDbPath);

        var flat = PfsFilesDbParser.Parse(filesDbPath);
        var unicv = PfsUnicvDbParser.Parse(unicvDbPath, flat.Count);

        var f00d = new VitaF00DEmulator();

        Directory.CreateDirectory(destPath);

        for (int i = 0; i < flat.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var entry = flat[i];
            string relativePath = entry.RelativePath ?? entry.Name;
            string srcFile = Path.Combine(titleIdPath, relativePath);
            string dstFile = Path.Combine(destPath, relativePath);

            if (entry.Type.IsDirectory())
            {
                Directory.CreateDirectory(dstFile);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(dstFile)!);

            if (!File.Exists(srcFile))
            {
                progress?.Report((double)(i + 1) / flat.Count);
                continue;
            }

            if (!entry.Type.IsEncrypted())
            {
                File.Copy(srcFile, dstFile, overwrite: true);
                progress?.Report((double)(i + 1) / flat.Count);

                continue;
            }

            var unicvEntry = unicv[i];
            byte[] data = File.ReadAllBytes(srcFile);

            if (unicvEntry.NSectors > 0 && data.Length > 0)
            {
                if (unicvEntry.HasDbSeed)
                {
                    var cipher = new VitaPfsGameDataCipher(f00d, klicensee, unicvEntry.DbSeed, (int)unicvEntry.FileSectorSize);
                    cipher.DecryptRange(0, data);
                }
                else
                    UnsupportedEntries.Add($"{relativePath} (table={unicvEntry.TableMagic}, dbseed 없음 - 레거시 키 유도 미구현, 원본 그대로 복사됨)");
            }

            File.WriteAllBytes(dstFile, data);
            progress?.Report((double)(i + 1) / flat.Count);
        }
    }
}