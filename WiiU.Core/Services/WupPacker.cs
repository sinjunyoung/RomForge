using System.Security.Cryptography;
using NUSPacker;
using NUSPacker.Nuspackage;
using NUSPacker.Nuspackage.Contents;
using NUSPacker.Nuspackage.Crypto;
using NUSPacker.Nuspackage.Fst;
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
/// The FST tree is built entirely IN MEMORY - input bytes are never staged to disk. Grouping is
/// decided by the same ContentRules regex rules the CLI tool uses (ContentRules.GetCommonRules),
/// walked directly against the in-memory tree (ContentRulesService never touches the filesystem).
///
/// The only disk I/O that's unavoidable is the packing algorithm's own scratch ".dec" files (one
/// per content, used while hashing/encrypting) and the final .app/.h3/title.* output - that part is
/// intrinsic to how content hashing and encryption work and isn't "extra" I/O.
/// </summary>
public static class WupPacker
{
    public static void Pack(
        string outputFolder,
        ulong titleId,
        ushort titleVersion,
        IReadOnlyList<WupContentGroup> groups,
        Action<ulong, ulong, string>? onProgress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputFolder);

        string scratchRoot = Path.Combine(Path.GetTempPath(), "romforge_pack_" + Guid.NewGuid().ToString("N"));
        string prevTmpDir = Settings.tmpDir;
        Settings.tmpDir = Path.Combine(scratchRoot, "tmp");

        try
        {
            Directory.CreateDirectory(Settings.tmpDir);

            ulong totalBytes = 0;
            foreach (var group in groups)
                foreach (var file in group.Files)
                    totalBytes += (ulong)file.Length;

            // ---- build the FST tree entirely in memory ----
            var contents = new Contents();
            var fst = new FST(contents);
            FSTEntry root = fst.GetFSTEntries().GetRootEntry()!;
            root.SetContent(contents.GetFSTContent());

            var dirsByPath = new Dictionary<string, FSTEntry>(StringComparer.Ordinal) { [""] = root };
            var seenFilePaths = new HashSet<string>(StringComparer.Ordinal);

            FSTEntry GetOrCreateDir(string dirPath)
            {
                if (dirsByPath.TryGetValue(dirPath, out var existing))
                    return existing;

                int slash = dirPath.LastIndexOf('/');
                string parentPath = slash < 0 ? "" : dirPath[..slash];
                string name = slash < 0 ? dirPath : dirPath[(slash + 1)..];

                FSTEntry parent = GetOrCreateDir(parentPath);
                var dir = new FSTEntry(false);
                dir.SetDir(true);
                dir.SetFileName(name);
                parent.AddChildren(dir);
                dirsByPath[dirPath] = dir;
                return dir;
            }

            byte[]? appXmlBytes = null;

            foreach (var group in groups)
            {
                foreach (var file in group.Files)
                {
                    ct.ThrowIfCancellationRequested();

                    string relPath = file.RelativePath.Trim('/');

                    if (!seenFilePaths.Add(relPath))
                        throw new InvalidOperationException($"같은 경로가 두 번 등장했습니다 (대소문자까지 포함해서 동일함): {relPath}");

                    if (string.Equals(relPath, "code/app.xml", StringComparison.Ordinal))
                    {
                        // app.xml is tiny (a few hundred bytes) - fine to read fully here, unlike
                        // content files which can be huge and must stay stream-based end to end.
                        using var s = file.OpenRead();
                        using var ms = new MemoryStream();
                        s.CopyTo(ms);
                        appXmlBytes = ms.ToArray();
                    }

                    int lastSlash = relPath.LastIndexOf('/');
                    string dirPath = lastSlash < 0 ? "" : relPath[..lastSlash];
                    string leafName = lastSlash < 0 ? relPath : relPath[(lastSlash + 1)..];

                    FSTEntry parentDir = GetOrCreateDir(dirPath);
                    var entry = new FSTEntry(leafName, file.OpenRead, file.Length);
                    parentDir.AddChildren(entry);
                }
            }

            // Same defaults Starter.cs uses on the command line, overridden by app.xml below if
            // present - mirrors what the CLI tool does by default (it parses app.xml unless told
            // not to).
            var appInfo = new AppXMLInfo();
            appInfo.SetTitleID((long)titleId);
            appInfo.SetGroupID((short)((titleId >> 8) & 0xFFFF));
            appInfo.SetOSVersion(0x000500101000400AL);
            appInfo.SetTitleVersion((short)titleVersion);
            appInfo.SetAppType(unchecked((int)0x80000000));

            if (appXmlBytes != null)
            {
                try
                {
                    var parser = new XMLParser();
                    using var ms = new MemoryStream(appXmlBytes);
                    parser.LoadDocument(ms);
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
            ContentRulesService.ApplyRules(root, contents, rules);

            byte[] titleKeyPlain = RandomNumberGenerator.GetBytes(16);
            var encryptionKey = new Key(titleKeyPlain);
            var encryptWithKey = new Key(Constants.WiiUCommonKey);

            ct.ThrowIfCancellationRequested();

            NUSPackage nusPackage = NUSPackageFactory.CreatePackageFromBuiltTree(contents, fst, appInfo, encryptionKey, encryptWithKey);

            // Each content is processed in three full passes (stage into scratch, hash, then
            // encrypt), so total "work" is roughly 3x the plain input size. Track how far each
            // content has gotten in each phase so per-block callbacks turn into one smoothly
            // increasing total.
            var contentLastDone = new Dictionary<(Content, string), long>();
            long totalWork = (long)totalBytes * 3;
            long doneWork = 0;

            onProgress?.Invoke(0, totalBytes, "패킹 중");

            nusPackage.PackContents(outputFolder, onContentPacked: null, onContentBytesProcessed: (content, phase, done, total) =>
            {
                ct.ThrowIfCancellationRequested();

                var key = (content, phase);
                contentLastDone.TryGetValue(key, out long prevDone);

                long delta = done - prevDone;
                if (delta < 0) delta = 0; // 혹시라도 리셋되는 경우 방지
                contentLastDone[key] = done;

                doneWork += delta;
                if (doneWork > totalWork) doneWork = totalWork;

                // 전체 작업량 대비 진행도를 안전하게 계산 (double 사용 후 ulong 변환으로 오버플로우 원천 차단)
                ulong reportedBytes = totalWork > 0 ? (ulong)((double)doneWork / totalWork * totalBytes) : totalBytes;

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