// WiiUKeyProvider.cs
//
// C# port of Cemu's actual keys.txt loader (src/Cafe/Filesystem/FST/KeyCache.cpp,
// function KeyCache_Prepare — verified directly against the cemu-project/Cemu source).
//
// The real algorithm is much simpler than community documentation suggests, and does NOT
// distinguish "common key" from "disc key" from "title key" at load time at all:
//   1. Truncate the line at the first '#' or ';'.
//   2. Strip every space, tab, '-' and '_' character from what remains (not just the ends —
//      every occurrence, anywhere in the line).
//   3. If what's left isn't pure hex, or isn't exactly 32 hex chars (16 bytes), skip the line.
//   4. Otherwise, add it as one more AES-128 key candidate to a single flat list.
//
// This means a line like "00050000101c9300 082fa2981e004defac03524cc685f693 # BotW JP" is
// NOT parsed as a titleId->key mapping — after whitespace stripping it becomes a 48-character
// string, which is rejected as an invalid key length and silently ignored. Only bare
// 32-hex-char keys (with or without a trailing "# comment") are ever used. This is intentional
// and matches Cemu's real behavior: every candidate key is tried by brute force wherever a key
// is needed (see WuDiscReader's FindDiscKey), verified via a structural check, not looked up by
// title ID.


namespace WiiU.Core.Services;

public sealed class WiiUKeyProvider
{
    /// <summary>Every valid 16-byte key candidate found in the file, in file order.</summary>
    public IReadOnlyList<byte[]> KeyCandidates => _keyCandidates;

    private readonly List<byte[]> _keyCandidates = new();

    private WiiUKeyProvider() { }

    public static WiiUKeyProvider LoadFromFile(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Wii U key file not found: {path}", path);

        var provider = new WiiUKeyProvider();

        foreach (var rawLine in File.ReadLines(path))
        {
            string line = rawLine;

            // truncate at first '#' or ';'
            int cut = line.Length;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '#' || line[i] == ';') { cut = i; break; }
            }
            line = line[..cut];

            // strip spaces, tabs, dashes, underscores (anywhere, not just at the ends)
            var stripped = new System.Text.StringBuilder(line.Length);
            foreach (char c in line)
            {
                if (c is ' ' or '\t' or '-' or '_') continue;
                stripped.Append(c);
            }
            string cleaned = stripped.ToString();
            if (cleaned.Length == 0) continue;

            if (!IsHex(cleaned)) continue;
            if (cleaned.Length != 32) continue; // only exact 128-bit keys are used

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
            if (!ok) return false;
        }
        return true;
    }
}
