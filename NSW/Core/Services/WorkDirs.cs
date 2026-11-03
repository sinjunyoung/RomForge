using System.IO;

namespace NSW.Core.Services;

public sealed class WorkDirs(string outputDir)
{
    public string Unpacked { get; } = Path.Combine(outputDir, "unpacked");

    public void Prepare()
    {
        if (Directory.Exists(Unpacked))
            Directory.Delete(Unpacked, true);

        Directory.CreateDirectory(Unpacked);
    }

    public void Cleanup()
    {
        try
        {
            if (Directory.Exists(Unpacked))
                Directory.Delete(Unpacked, true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"언팩 디렉터리 삭제 실패 ({Unpacked}): {ex.Message}");
        }
    }
}