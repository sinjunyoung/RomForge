namespace WiiU.Core.Services;

public interface ITitleSource : IDisposable
{
    string TitleIdHex { get; }

    int TitleVersion { get; }

    IEnumerable<string> EnumerateFiles();

    Stream OpenRead(string path);

    long GetFileSize(string path);
}