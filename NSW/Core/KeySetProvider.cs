using LibHac.Common.Keys;
using System.Diagnostics;
using System.IO;

namespace NSW.Core;

public sealed class KeySetProvider
{
    private static readonly Lock _lock = new();


    public KeySet KeySet
    {
        set;
        get
        {
            if (field == null) TryLoadKeys();
            return field ?? throw new NullReferenceException();
        }
    }

    public static KeySetProvider Instance
    {
        get
        {
            lock (_lock) return field ??= new KeySetProvider();
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
                KeySet = ExternalKeyReader.ReadKeyFile(keysPath); 
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"prod.keys 로딩 실패 ({keysPath}): {ex.Message}");
            }
        }
    }
}