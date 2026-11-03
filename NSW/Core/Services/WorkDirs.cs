using System.IO;

namespace NSW.Core.Services;

public sealed class WorkDirs(string outputDir)
{
    public string Unpacked { get; } = Path.Combine(outputDir, "unpacked");

    public string Temp { get; } = Path.Combine(outputDir, "temp");

    public void Prepare()
    {
        foreach (var dir in new[] { Unpacked, Temp })
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }

        Directory.CreateDirectory(Unpacked);
    }

    public void Cleanup(bool keepUnpacked = false)
    {
        string[] targets = keepUnpacked ? [Temp] : [Temp, Unpacked];

        foreach (var dir in targets)
        {
            try
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"임시 디렉터리 삭제 실패 ({dir}): {ex.Message}");
            }
        }
    }
}