using System.Text.Json;

namespace WiiGC.Core.Models;

public sealed record WiiFolderInfo(int Version, long ContainerOffset, long DiscLength, string GameId, string Title)
{
    public const int CurrentVersion = 1;

    public const string FileName = "info.json";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string GetPath(string unpackedFolder) => Path.Combine(unpackedFolder, "meta", FileName);

    public static WiiFolderInfo? TryRead(string unpackedFolder)
    {
        try
        {
            string path = GetPath(unpackedFolder);

            if (!File.Exists(path))
                return null;

            var info = JsonSerializer.Deserialize<WiiFolderInfo>(File.ReadAllText(path), Options);

            return info is { Version: CurrentVersion } ? info : null;
        }
        catch
        {
            return null;
        }
    }

    public static WiiFolderInfo Read(string unpackedFolder) => TryRead(unpackedFolder) ?? throw new InvalidDataException("언팩 폴더 정보를 읽을 수 없습니다. 이 버전에서 다시 언팩해 주세요.");

    public void Write(string unpackedFolder)
    {
        string path = GetPath(unpackedFolder);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
    }
}