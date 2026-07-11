using System.IO;
using WiiU.Core.Services;

namespace RomForge.Core.Services.WiiU;

public sealed class UnpackService
{
    public static ITitleSource Open(string inputPath, string? keysTxtPath = null)
    {
        if (!File.Exists(inputPath))
            throw new FileNotFoundException($"Input file not found: {inputPath}", inputPath);

        string ext = Path.GetExtension(inputPath).ToLowerInvariant();

        switch (ext)
        {
            case ".wua":
                return new WuaTitleSource(WuaReader.OpenFromFile(inputPath));

            case ".wud":
            case ".wux":
            {
                if (string.IsNullOrEmpty(keysTxtPath))
                    throw new ArgumentException("keysTxtPath is required to unpack a .wud/.wux file.", nameof(keysTxtPath));

                var keys = KeyProvider.LoadFromFile(keysTxtPath);
                var wud = WudReader.Open(inputPath);

                try
                {
                        byte[]? discKey = WuDiscReader.FindDiscKey(wud, inputPath, keys.KeyCandidates) ?? throw new InvalidDataException(
                            "Could not find a working disc key. None of the candidates in keys.txt matched, " + 
                            "and no companion \"<name>.key\" file was found next to the input.");
                        var disc = WuDiscReader.Open(wud, discKey);

                    return new WudTitleSource(wud, disc);
                }
                catch
                {
                    wud.Dispose();
                    throw;
                }
            }

            default:
                throw new NotSupportedException($"Unsupported input file type: \"{ext}\". Expected .wud, .wux, or .wua.");
        }
    }
}