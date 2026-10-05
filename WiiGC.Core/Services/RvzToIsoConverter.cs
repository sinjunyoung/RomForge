namespace WiiGC.Core.Services;

public static class RvzToIsoConverter
{
    public static void Convert(string inputPath, string outputPath, Action<double>? progress = null, CancellationToken ct = default)
    {
        bool succeeded = false;

        try
        {
            using var reader = new RvzDiscReader(inputPath);
            using var output = SparseFile.Create(outputPath, reader.IsoSize);

            reader.WriteIso(new FileIsoSink(output), progress, ct);
            
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