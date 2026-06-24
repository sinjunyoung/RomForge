using PBP.Core.Models;

namespace PBP.Core.Services;

public static class CueFileWriter
{
    public static void Write(CueFile cueFile, string path)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        using var writer = new StreamWriter(stream);

        foreach (var entry in cueFile.Entries)
        {
            writer.WriteLine($"FILE \"{entry.FileName}\" {entry.FileType}");

            foreach (var track in entry.Tracks)
            {
                writer.WriteLine($"  TRACK {track.Number:00} {track.DataType}");

                foreach (var index in track.Indexes)
                    writer.WriteLine($"    INDEX {index.Number:00} {index.Position}");
            }
        }

        writer.Flush();
    }
}