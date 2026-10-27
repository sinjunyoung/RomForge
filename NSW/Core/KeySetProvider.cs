using LibHac.Common.Keys;
using System.Diagnostics;
using System.IO;

namespace NSW.Core;

public sealed class KeySetProvider
{
    private static KeySetProvider _instance;
    private static readonly Lock _lock = new();

    private KeySet _keySet;

    public KeySet KeySet
    {
        get
        {
            if (_keySet == null) TryLoadKeys();
            return _keySet;
        }
    }

    public static KeySetProvider Instance
    {
        get
        {
            lock (_lock) return _instance ??= new KeySetProvider();
        }
    }

    private KeySetProvider() { }

    public void TryLoadKeys()
    {
        string keysPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "prod.keys");

        if (File.Exists(keysPath))
        {
            try 
            { 
                _keySet = ExternalKeyReader.ReadKeyFile(keysPath); 
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"prod.keys 로딩 실패 ({keysPath}): {ex.Message}");
            }
        }
    }
}