namespace NSW.Core;

public static class ExperimentalFlags
{
    public static bool StreamCompressRomfs { get; } = Environment.GetEnvironmentVariable("ROMFORGE_STREAM_LZ4") == "1";
}