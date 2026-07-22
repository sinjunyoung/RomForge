using System.Security.Cryptography;
using WiiU.Core.Models;
using WiiU.Core.WUP;
using WiiU.Core.WUP.Crypto;
using WiiU.Core.WUP.Models;
using WiiU.Core.WUP.Services;

namespace WiiU.Core.Services;

public static class WupPacker
{
    public static void Pack(string outputFolder, ulong titleId, ushort titleVersion, IReadOnlyList<WupContentGroup> groups, Action<ulong, ulong, string>? onProgress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputFolder);

        string scratchRoot = Path.Combine(Path.GetTempPath(), "romforge_pack_" + Guid.NewGuid().ToString("N"));
        string prevTmpDir = Settings.TmpDir;
        Settings.TmpDir = Path.Combine(scratchRoot, "tmp");

        try
        {
            Directory.CreateDirectory(Settings.TmpDir);

            ulong totalBytes = 0;

            foreach (var group in groups)
                foreach (var file in group.Files)
                    totalBytes += (ulong)file.Length;

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
                        using var s = file.OpenRead();
                        using var ms = new MemoryStream();

                        s.CopyTo(ms);

                        appXmlBytes = ms.ToArray();
                    }

                    int lastSlash = relPath.LastIndexOf('/');
                    string dirPath = lastSlash < 0 ? string.Empty : relPath[..lastSlash];
                    string leafName = lastSlash < 0 ? relPath : relPath[(lastSlash + 1)..];
                    FSTEntry parentDir = GetOrCreateDir(dirPath);

                    var entry = new FSTEntry(leafName, file.OpenRead, file.Length);
                    parentDir.AddChildren(entry);
                }
            }

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
                catch { }
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

                if (delta < 0) 
                    delta = 0;

                contentLastDone[key] = done;

                doneWork += delta;

                if (doneWork > totalWork) 
                    doneWork = totalWork;

                ulong reportedBytes = totalWork > 0 ? (ulong)((double)doneWork / totalWork * totalBytes) : totalBytes;

                onProgress?.Invoke(reportedBytes, totalBytes, $"{phase} #{content.GetID():x8}");
            });

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
            Settings.TmpDir = prevTmpDir;

            try 
            { 
                if (Directory.Exists(scratchRoot)) 
                    Directory.Delete(scratchRoot, true);
            } 
            catch {}
        }
    }
}