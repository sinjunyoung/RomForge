using LibHac.Common.Keys;
using System.Diagnostics;
using System.IO;

namespace NSW.Core;

public sealed class KeySetProvider
{
    private static readonly Lock _lock = new();

    public string? CustomKeysPath { get; private set; }

    public string KeysPath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(CustomKeysPath) && File.Exists(CustomKeysPath))
                return CustomKeysPath;

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "prod.keys");
        }
    }

    public KeySet? KeySet
    {
        set;
        get
        {
            if (field == null)
                TryLoadKeys();

            return field;
        }
    }

    public static KeySetProvider Instance
    {
        get
        {
            lock (_lock)
                return field ??= new KeySetProvider();
        }
    }

    private KeySetProvider() { }

    public void SetCustomKeysPath(string? path)
    {
        CustomKeysPath = path;
        KeySet = null;
    }

    public void TryLoadKeys()
    {
        string keysPath = KeysPath;

        if (File.Exists(keysPath))
        {
            try
            {
                KeySet = ExternalKeyReader.ReadKeyFile(keysPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"prod.keys 로딩 실패 ({keysPath}): {ex.Message}");
            }
        }
    }
}