namespace N3DS.Core.Crypto;

public sealed class KeyStoreProvider
{
    private static readonly Lock _lock = new();

    public KeyStore KeyStore
    {
        set;
        get
        {
            lock (_lock) return field ??= new KeyStore();
        }
    }

    public static KeyStoreProvider Instance
    {
        get
        {
            lock (_lock) return field ??= new KeyStoreProvider();
        }
    }

    private KeyStoreProvider() { }

    public void Reload()
    {
        lock (_lock) KeyStore = new KeyStore();
    }
}