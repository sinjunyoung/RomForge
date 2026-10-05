using WiiGC.Core.Services;

namespace WiiGC.Core.Services.Wii;

public static class WbfsToIsoConverter
{
    public static void Convert(string inputPath, string outputPath, Action<double>? progress = null, CancellationToken ct = default)
    {
        bool succeeded = false;

        try
        {
            using var input = RvzInputSource.Open(inputPath);

            if (input is not WbfsSource wbfs)
                throw new InvalidDataException("WBFS 파일이 아닙니다.");

            long length = wbfs.Length;
            int blockSize = wbfs.BlockSize;
            long blockCount = (length + blockSize - 1) / blockSize;
            using var output = SparseFile.Create(outputPath, length);

            RandomAccess.SetLength(output, length);

            byte[] buffer = new byte[blockSize];

            for (long block = 0; block < blockCount; block++)
            {
                ct.ThrowIfCancellationRequested();

                if (wbfs.IsBlockMapped(block))
                {
                    long offset = block * blockSize;
                    int size = (int)Math.Min(blockSize, length - offset);
                    var span = buffer.AsSpan(0, size);

                    wbfs.Read(offset, span);

                    if (span.ContainsAnyExcept((byte)0))
                        RandomAccess.Write(output, span, offset);
                }

                progress?.Invoke(Math.Min(1.0, (double)(block + 1) / blockCount));
            }

            succeeded = true;
        }
        finally
        {
            if (!succeeded)
            {
                try
                {
                    if (File.Exists(outputPath))
                        File.Delete(outputPath);
                }
                catch { }
            }
        }
    }
}