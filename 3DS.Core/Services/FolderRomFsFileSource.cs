using _3DS.Core.Interfaces;

namespace _3DS.Core.Services;

public class FolderRomFsFileSource(string folder, IRomFsFileSource? patchSource = null) : IRomFsFileSource
{
    public async ValueTask<Stream?> OpenFileAsync(string fullPath, Func<CancellationToken, ValueTask<Stream?>>? getOriginal = null, CancellationToken ct = default)
    {
        string localPath = Path.Combine(folder, fullPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

        ValueTask<Stream?> localGetOriginal(CancellationToken _) =>
            File.Exists(localPath)
                ? ValueTask.FromResult<Stream?>(File.OpenRead(localPath))
                : ValueTask.FromResult<Stream?>(null);

        if (patchSource != null)
        {
            var patchStream = await patchSource.OpenFileAsync(fullPath, localGetOriginal, ct);

            if (patchStream != null)
                return patchStream;
        }

        if (!File.Exists(localPath))
            return null;

        return File.OpenRead(localPath);
    }
}