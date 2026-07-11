namespace WiiU.Core.Services;

public sealed class KeyProvider
{
    public IReadOnlyList<byte[]> KeyCandidates => _keyCandidates;

    private readonly List<byte[]> _keyCandidates = [];

    private KeyProvider() { }

    public static KeyProvider LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Wii U key file not found: {path}", path);

        var provider = new KeyProvider();

        foreach (var rawLine in File.ReadLines(path))
        {
            string line = rawLine;
            int cut = line.Length;

            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '#' || line[i] == ';') 
                { 
                    cut = i; 
                    break; 
                }
            }

            line = line[..cut];

            var stripped = new System.Text.StringBuilder(line.Length);

            foreach (char c in line)
            {
                if (c is ' ' or '\t' or '-' or '_') 
                    continue;

                stripped.Append(c);
            }

            string cleaned = stripped.ToString();

            if (cleaned.Length == 0) 
                continue;

            if (!IsHex(cleaned)) 
                continue;

            if (cleaned.Length != 32) 
                continue;

            var keyBytes = new byte[16];

            for (int i = 0; i < 16; i++)
                keyBytes[i] = Convert.ToByte(cleaned.Substring(i * 2, 2), 16);

            provider._keyCandidates.Add(keyBytes);
        }

        return provider;
    }

    private static bool IsHex(string s)
    {
        foreach (char c in s)
        {
            bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

            if (!ok)
                return false;
        }

        return true;
    }
}