using System.Security.Cryptography;
using NUSPacker;
using NUSPacker.Nuspackage.Contents;
using NUSPacker.Nuspackage.Crypto;
using NUSPacker.Nuspackage.Packaging;
using NUSPacker.Utils;
using WiiU.Core.Models;

namespace WiiU.Core.Services;

/// <summary>
/// Packs a title into WUP format (.app/.h3/title.tmd/title.cert/title.tik).
///
/// This is a thin adapter over NUSPackerSharp (a verified line-for-line C# port of the original
/// NUSPacker.jar - diffed byte-for-byte against the real jar's output; matches exactly except for
/// the two genuinely-random padding regions in title.tik).
///
/// Deliberately does NOT do any custom content-grouping/tree-building. It writes the files to a
/// scratch folder and calls NUSPackageFactory.CreateNewPackage() with ContentRules.GetCommonRules()
/// - the exact same call the CLI tool (Starter.cs) makes, which is the part that was actually
/// verified byte-identical against the real jar. The WupContentGroup list's Hashed/FstFlags fields
/// are intentionally ignored; grouping is decided by the same regex rules the real tool uses.
/// </summary>
public static class WupPacker
{
    public static void Pack(
        string outputFolder,
        ulong titleId,
        ushort titleVersion,
        IReadOnlyList<WupContentGroup> groups,
        Action<long, long, string>? onProgress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputFolder);

        string scratchRoot = Path.Combine(Path.GetTempPath(), "romforge_pack_" + Guid.NewGuid().ToString("N"));
        string sourceTree = Path.Combine(scratchRoot, "src");
        string prevTmpDir = Settings.tmpDir;
        Settings.tmpDir = Path.Combine(scratchRoot, "tmp");

        try
        {
            Directory.CreateDirectory(sourceTree);
            Directory.CreateDirectory(Settings.tmpDir);

            long totalBytes = 0;

            foreach (var group in groups)
            {
                foreach (var file in group.Files)
                {
                    ct.ThrowIfCancellationRequested();

                    string diskPath = Path.Combine(sourceTree, file.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(diskPath)!);
                    File.WriteAllBytes(diskPath, file.Data);
                    totalBytes += file.Data.LongLength;
                }
            }

            // Same defaults Starter.cs uses on the command line, overridden by app.xml below if present -
            // exactly mirroring what the CLI tool does by default (it parses app.xml unless told not to).
            var appInfo = new AppXMLInfo();
            appInfo.SetTitleID((long)titleId);
            appInfo.SetGroupID((short)((titleId >> 8) & 0xFFFF));
            appInfo.SetOSVersion(0x000500101000400AL);
            appInfo.SetTitleVersion((short)titleVersion);
            appInfo.SetAppType(unchecked((int)0x80000000));

            string appXmlPath = Path.Combine(sourceTree, "code", "app.xml");
            if (File.Exists(appXmlPath))
            {
                try
                {
                    var parser = new XMLParser();
                    parser.LoadDocument(appXmlPath);
                    appInfo = parser.GetAppXMLInfo();
                }
                catch
                {
                    // Same as the CLI: if app.xml parsing fails, fall back to the defaults set above.
                }
            }

            short contentGroup = appInfo.GetGroupID();
            long parentTitleId = appInfo.GetTitleID() & ~0x0000000F00000000L;

            var rules = ContentRules.GetCommonRules(contentGroup, parentTitleId);

            byte[] titleKeyPlain = RandomNumberGenerator.GetBytes(16);
            var encryptionKey = new Key(titleKeyPlain);
            var encryptWithKey = new Key(Constants.WiiUCommonKey);

            var config = new NusPackageConfiguration(sourceTree, appInfo, encryptionKey, encryptWithKey, rules);

            NUSPackage nusPackage = NUSPackageFactory.CreateNewPackage(config);

            // Each content is processed in two full passes (hash, then encrypt), so total "work"
            // is roughly 2x the plain input size. Track how far each content has gotten in each
            // phase so we can turn per-block callbacks into a single smoothly increasing total.
            var contentProgress = new Dictionary<Content, (long hash, long encrypt)>();
            long totalWork = totalBytes * 2;
            long doneWork = 0;

            onProgress?.Invoke(0, totalBytes, "패킹 중");

            nusPackage.PackContents(outputFolder, onContentPacked: null, onContentBytesProcessed: (content, phase, done, total) =>
            {
                ct.ThrowIfCancellationRequested();

                contentProgress.TryGetValue(content, out var prev);
                long delta;
                if (phase == "hash")
                {
                    delta = done - prev.hash;
                    prev.hash = done;
                }
                else
                {
                    delta = done - prev.encrypt;
                    prev.encrypt = done;
                }
                contentProgress[content] = prev;

                doneWork += delta;
                long reportedBytes = Math.Min(doneWork / 2, totalBytes);
                onProgress?.Invoke(reportedBytes, totalBytes, $"{phase} #{content.GetID():x8}");
            });

            // NUSPackerSharp writes content filenames in uppercase hex (ported from the original
            // Java CLI's %08X convention). The rest of this codebase (WupTitleSource etc.) expects
            // lowercase hex ({cid:x8}.app / .h3). Normalize here so nothing downstream has to care.
            //
            // On case-insensitive filesystems (Windows/macOS default), the uppercase and lowercase
            // paths refer to the SAME physical file, so renaming through a temp name first avoids
            // the source colliding with (and disappearing under) its own destination.
            foreach (var file in Directory.GetFiles(outputFolder, "*.app").Concat(Directory.GetFiles(outputFolder, "*.h3")))
            {
                string fileName = Path.GetFileName(file);
                string lower = fileName.ToLowerInvariant();

                if (fileName == lower)
                    continue;

                string dest = Path.Combine(outputFolder, lower);
                string tempPath = file + ".renaming_tmp";

                File.Move(file, tempPath, overwrite: true);
                File.Move(tempPath, dest, overwrite: true);
            }

            onProgress?.Invoke(totalBytes, totalBytes, "완료");
        }
        finally
        {
            Settings.tmpDir = prevTmpDir;
            try { if (Directory.Exists(scratchRoot)) Directory.Delete(scratchRoot, true); } catch { /* best effort */ }
        }
    }
}