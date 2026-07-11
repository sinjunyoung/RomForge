using _3DS.Core.Interfaces;
using Patch.Core;

namespace _3DS.Core.Services;

public class PatchFolderFileSource(string patchFolder) : IRomFsFileSource
{
    private static readonly string[] PatchExtensions = [".xdelta", ".ips", ".bps", ".ups", ".ppf", ".aps"];

    public async ValueTask<Stream?> OpenFileAsync(string fullPath, Func<CancellationToken, ValueTask<Stream?>>? getOriginal = null, CancellationToken ct = default)
    {
        string relative = fullPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        string localPath = Path.Combine(patchFolder, relative);

        if (File.Exists(localPath))
            return File.OpenRead(localPath);

        foreach (string ext in PatchExtensions)
        {
            string patchFilePath = localPath + ext;

            if (!File.Exists(patchFilePath))
                continue;

            if (getOriginal == null)
                throw new InvalidOperationException($"패치 파일을 적용하려면 원본 데이터가 필요하지만 제공되지 않았습니다: {relative}{ext}");

            var originalStream = await getOriginal(ct) ?? throw new FileNotFoundException($"원본 파일을 찾을 수 없어 패치를 적용할 수 없습니다: {relative}");
            byte[] originalData;

            await using (originalStream)
            {
                using var ms = new MemoryStream();

                await originalStream.CopyToAsync(ms, ct);
                originalData = ms.ToArray();
            }

            byte[] patchData = await File.ReadAllBytesAsync(patchFilePath, ct);
            byte[] patchedData = await UniversalPatcher.ApplyPatchAsync(originalData, patchData, null, ct);

            return new MemoryStream(patchedData);
        }

        return null;
    }
}