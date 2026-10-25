namespace Patch.Core.Models;

public readonly struct MaterializedTempFile(string path, bool owned) : IDisposable
{
    public string Path { get; } = path;

    public static MaterializedTempFile Wrap(string path) => new(path, false);

    public static MaterializedTempFile Owned(string path) => new(path, true);

    public void Dispose()
    {
        if (owned && File.Exists(Path))
        {
            try
            {
                File.Delete(Path);
            }
            catch { }
        }
    }
}