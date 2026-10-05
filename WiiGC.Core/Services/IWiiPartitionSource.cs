using WiiGC.Core.Models;

namespace WiiGC.Core.Services;

internal interface IWiiPartitionSource
{
    bool TryReadDecryptedHashGroup(long readOffset, int blocksInThisGroup, byte[] decrypted, List<HashException> exceptions);
}