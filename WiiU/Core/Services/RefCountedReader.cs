namespace WiiU.Core.Services;

internal sealed class RefCountedReader(WuaReader reader, int initialRefCount)
{
    public WuaReader Reader => reader;

    public void Release()
    {
        if (Interlocked.Decrement(ref initialRefCount) <= 0)
            reader.Dispose();
    }
}