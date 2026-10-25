namespace PSP.PBP.Services;

public sealed class ResolvedDisc : IDisposable
{
    public required Stream IsoStream { get; init; }

    public required long IsoLength { get; init; }

    public required byte[] TocData { get; init; }

    private string? _tempFile;

    public void Dispose()
    {
        IsoStream.Dispose();

        if (_tempFile != null && File.Exists(_tempFile))
        {
            try
            {
                File.Delete(_tempFile);
            } 
            catch { }
        }
    }

    internal static ResolvedDisc Create(Stream s, long len, byte[] toc, string? temp = null) => new() { IsoStream = s, IsoLength = len, TocData = toc, _tempFile = temp };
}