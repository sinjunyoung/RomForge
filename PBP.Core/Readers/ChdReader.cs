using CHD.Core.Interop;
using CHD.Core.Interop.Enums;

namespace PBP.Core.Readers;

public class ChdReader : IDiskReader, IDisposable
{
    private readonly LibChdrWrapper _chd;
    private bool _disposed;

    public int SectorSize => 2352;

    private readonly int _sectorsPerHunk;
    private readonly uint _totalHunks;

    public long TotalSectors { get; }

    public ChdReader(string chdPath)
    {
        _chd = new LibChdrWrapper();
        var err = _chd.Open(chdPath);

        if (err != ChdrError.CHDERR_NONE)
            throw new IOException($"CHD 열기 실패: {LibChdrWrapper.GetErrorString(err)} ({chdPath})");

        var header = _chd.Header!.Value;

        _totalHunks = header.totalhunks;
        _sectorsPerHunk = (int)(header.hunkbytes / SectorSize);

        if (_sectorsPerHunk == 0) _sectorsPerHunk = 1;

        TotalSectors = _totalHunks * _sectorsPerHunk;
    }

    public byte[] ReadSectors(long startSector, int count)
    {
        var result = new byte[count * SectorSize];
        int resultOffset = 0;
        long remaining = count;
        long currentSector = startSector;

        while (remaining > 0)
        {
            uint hunkIndex = (uint)(currentSector / _sectorsPerHunk);
            int offsetInHunk = (int)(currentSector % _sectorsPerHunk);

            if (hunkIndex >= _totalHunks) 
                break;

            var hunkData = _chd.ReadHunk(hunkIndex);

            int canRead = (int)Math.Min(remaining, _sectorsPerHunk - offsetInHunk);
            int srcOffset = offsetInHunk * SectorSize;
            int copyLen = canRead * SectorSize;

            Buffer.BlockCopy(hunkData, srcOffset, result, resultOffset, copyLen);

            resultOffset += copyLen;
            currentSector += canRead;
            remaining -= canRead;
        }

        return result;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _chd.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
