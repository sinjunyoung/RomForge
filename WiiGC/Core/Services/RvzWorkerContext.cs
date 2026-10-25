using WiiGC.Core.Enums;
using WiiGC.Core.Models;
using WiiGC.Core.Models.Wii;
using WiiGC.Core.Services.Wii;

namespace WiiGC.Core.Services;

internal class RvzWorkerContext(RvzCompressionType compression, byte[] compressorData) : IDisposable
{
    private byte[]? _decrypted;
    private byte[]? _encrypted;
    private GroupEncryptor? _encryptor;

    public RvzChunkDecoder Decoder { get; } = new(compression, compressorData);

    public List<HashException> Exceptions { get; } = [];

    public byte[] Input { get; private set; } = [];

    public byte[] Output { get; private set; } = [];

    public byte[] Decrypted => _decrypted ??= new byte[Layout.GroupDataSize];

    public byte[] Encrypted => _encrypted ??= new byte[Layout.GroupTotalSize];

    public GroupEncryptor Encryptor => _encryptor ??= new GroupEncryptor();

    public long CachedGroupIndex { get; set; } = -1;

    public DecodedChunk? CachedChunk { get; set; }

    public void EnsureInput(int size)
    {
        if (Input.Length < size)
            Input = new byte[size];
    }

    public void EnsureOutput(int size)
    {
        if (Output.Length < size)
            Output = new byte[size];
    }

    public void Dispose()
    {
        Decoder.Dispose();
        _encryptor?.Dispose();
    }
}