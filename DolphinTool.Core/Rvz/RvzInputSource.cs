using DolphinTool.Core.Services.GameCube;
using DolphinTool.Core.Services.Wbfs;
using DolphinTool.Core.Services.Wia;

namespace DolphinTool.Core.Rvz;

internal static class RvzInputSource
{
    public static IRvzInputSource Open(string path)
    {
        var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan);

        try
        {
            if (GczSource.IsGcz(handle))
                return new GczSource(handle);

            if (WbfsSource.IsWbfs(handle))
                return WbfsSource.Open(path, handle);

            if (WiaSource.IsWia(handle))
                return WiaSource.Open(handle);

            return new PlainFileSource(handle);
        }
        catch
        {
            handle.Dispose();

            throw;
        }
    }
}