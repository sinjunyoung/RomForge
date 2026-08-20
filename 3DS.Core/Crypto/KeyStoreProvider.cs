namespace _3DS.Core.Crypto;

public sealed class KeyStoreProvider
{
    private static KeyStoreProvider _instance;
    private static readonly object _lock = new();

    private KeyStore _keyStore;

    public KeyStore KeyStore
    {
        get
        {
            lock (_lock) return _keyStore ??= new KeyStore();
        }
    }

    public static KeyStoreProvider Instance
    {
        get
        {
            lock (_lock) return _instance ??= new KeyStoreProvider();
        }
    }

    private KeyStoreProvider() { }

    public void Reload()
    {
        lock (_lock) _keyStore = new KeyStore();
    }
}