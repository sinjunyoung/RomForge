namespace PBP.Core.Services;

public class PbpUnpacker
{
    public Action<int>? OnProgress { get; set; }

    public Action<string>? OnNotify { get; set; }

    public async Task UnpackAsync(string pbpPath, string outputDir, bool createCuesheet = true, CancellationToken ct = default)
    {
        using var stream = new FileStream(pbpPath, FileMode.Open, FileAccess.Read);
        var reader = new PbpReader(stream);

        Directory.CreateDirectory(outputDir);

        var baseName = Path.GetFileNameWithoutExtension(pbpPath);
        var isMultiDisc = reader.Discs.Count > 1;
        var totalSize = reader.Discs.Sum(d => d.IsoSize);
        long baseSize = 0;

        var createdFiles = new List<string>();

        try
        {
            foreach (var disc in reader.Discs)
            {
                ct.ThrowIfCancellationRequested();

                var capturedBase = baseSize;

                disc.ProgressEvent = bytes => OnProgress?.Invoke((int)Math.Round((capturedBase + bytes) * 100.0 / totalSize));

                var discSuffix = isMultiDisc ? $" (Disc {disc.Index})" : string.Empty;
                var candidateBaseName = $"{baseName}{discSuffix}";
                var (binPath, cuePath) = GetUniquePairedPaths(outputDir, candidateBaseName);

                OnNotify?.Invoke($"언팩중 {binPath}...");
                createdFiles.Add(binPath);

                await Task.Run(() =>
                {
                    using var binStream = new FileStream(binPath, FileMode.Create, FileAccess.Write);

                    disc.CopyTo(binStream, ct);
                }, ct);

                if (createCuesheet)
                {
                    var cueFile = TocHelper.TOCtoCUE(disc.TOC, Path.GetFileName(binPath));

                    createdFiles.Add(cuePath);
                    CueFileWriter.Write(cueFile, cuePath);
                    OnNotify?.Invoke($"언팩중 {cuePath}...");
                }

                baseSize += disc.IsoSize;
            }
        }
        catch (Exception)
        {
            OnNotify?.Invoke("실패 - 생성된 파일 정리 중...");

            foreach (var path in createdFiles)
                try 
                {
                    File.Delete(path); 
                }
                catch { }

            throw;
        }

        OnNotify?.Invoke("언팩 완료");
    }

    private static (string BinPath, string CuePath) GetUniquePairedPaths(string outputDir, string baseName)
    {
        var candidate = baseName;
        var suffix = 0;

        while (true)
        {
            var binPath = Path.Combine(outputDir, $"{candidate}.bin");
            var cuePath = Path.Combine(outputDir, $"{candidate}.cue");

            if (!File.Exists(binPath) && !File.Exists(cuePath))
                return (binPath, cuePath);

            suffix++;
            candidate = $"{baseName}{new string('_', suffix)}";
        }
    }
}