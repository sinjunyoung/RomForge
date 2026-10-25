using System.IO;

namespace NSW.Core.Services;

public sealed class WorkDirs(string outputDir)
{
    public string Unpacked { get; } = Path.Combine(outputDir, "unpacked");

    public string Temp { get; } = Path.Combine(outputDir, "temp");

    public string BuildNca { get; } = Path.Combine(outputDir, "build_nca");

    public void Prepare()
    {
        foreach (var dir in new[] { Unpacked, Temp, BuildNca })
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);

            Directory.CreateDirectory(dir);
        }
    }

    public void Cleanup(bool keepUnpacked = false)
    {
        string [] targets = keepUnpacked ? [BuildNca, Temp] : [ BuildNca, Temp, Unpacked ];

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