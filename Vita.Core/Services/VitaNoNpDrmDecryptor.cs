using Vita.Core.Cryptography;
using Vita.Core.Models;

namespace Vita.Core.Services;

public static class VitaNoNpDrmDecryptor
{
    public static VitaPfsFileTable ParseFileTable(string titleIdPath)
    {
        using var accessor = new FolderSourceAccessor(titleIdPath);

        return ParseFileTable(accessor, string.Empty);
    }

    public static VitaPfsFileTable ParseFileTable(IVitaSourceAccessor accessor, string titleRelPath)
    {
        string filesDbRel = Combine(titleRelPath, "sce_pfs/files.db");
        string unicvDbRel = Combine(titleRelPath, "sce_pfs/unicv.db");

        if (!accessor.FileExists(filesDbRel))
            throw new FileNotFoundException("files.db를 찾을 수 없습니다.", filesDbRel);

        if (!accessor.FileExists(unicvDbRel))
            throw new FileNotFoundException("unicv.db를 찾을 수 없습니다.", unicvDbRel);

        using var filesDbStream = new MemoryStream(accessor.ReadAllBytes(filesDbRel));
        var flat = PfsFilesDbParser.Parse(filesDbStream, out uint filesSalt);
        using var unicvDbStream = new MemoryStream(accessor.ReadAllBytes(unicvDbRel));
        var unicv = PfsUnicvDbParser.Parse(unicvDbStream, flat.Count);

        return new VitaPfsFileTable { Entries = flat, UnicvEntries = unicv, FilesSalt = filesSalt };
    }

    public static byte[] DecryptEntry(string titleIdPath, byte[] klicensee, PfsFlatEntry entry, PfsUnicvEntry unicvEntry, uint filesSalt, out string? warning)
    {
        using var accessor = new FolderSourceAccessor(titleIdPath);

        return DecryptEntry(accessor, string.Empty, klicensee, entry, unicvEntry, filesSalt, out warning);
    }

    public static byte[] DecryptEntry(IVitaSourceAccessor accessor, string titleRelPath, byte[] klicensee, PfsFlatEntry entry, PfsUnicvEntry unicvEntry, uint filesSalt, out string? warning)
    {
        warning = null;

        string relativePath = entry.RelativePath ?? entry.Name;
        string srcRel = Combine(titleRelPath, relativePath);
        byte[] data = accessor.ReadAllBytes(srcRel);

        if (!entry.Type.IsEncrypted())
            return data;

        if (unicvEntry.NSectors > 0 && data.Length > 0)
        {
            var f00d = new VitaF00DEmulator();

            if (unicvEntry.HasDbSeed)
            {
                var cipher = new VitaPfsGameDataCipher(f00d, klicensee, unicvEntry.DbSeed, (int)unicvEntry.FileSectorSize);

                cipher.DecryptRange(0, data);
            }
            else if (unicvEntry.TableMagic == "SCEIFTBL")
            {
                byte[] tweakEncKey = VitaPfsLegacyKeyDerivation.ComputeTweakEncKey(filesSalt, (uint)unicvEntry.PageNumber);
                var cipher = VitaPfsGameDataCipher.FromPrecomputedTweakKey(f00d, klicensee, tweakEncKey, (int)unicvEntry.FileSectorSize);

                cipher.DecryptRange(0, data);
            }
        }

        return data;
    }

    public static List<string> Decrypt(string titleIdPath, string destPath, byte[] klicensee, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var table = ParseFileTable(titleIdPath);
        var warnings = new List<string>();

        Directory.CreateDirectory(destPath);

        for (int i = 0; i < table.Entries.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var entry = table.Entries[i];
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
                progress?.Report((double)(i + 1) / table.Entries.Count);
                continue;
            }

            byte[] data = DecryptEntry(titleIdPath, klicensee, entry, table.UnicvEntries[i], table.FilesSalt, out string? warning);

            if (warning != null)
                warnings.Add(warning);

            File.WriteAllBytes(dstFile, data);
            progress?.Report((double)(i + 1) / table.Entries.Count);
        }

        return warnings;
    }

    private static string Combine(string basePath, string relative) => string.IsNullOrEmpty(basePath) ? relative : $"{basePath.TrimEnd('/')}/{relative.Replace('\\', '/')}";
}