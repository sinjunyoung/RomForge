using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Crypto;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Spl;
using LibHac.Tools.Crypto;
using LibHac.Tools.FsSystem.RomFs;
using KeyType = LibHac.Common.Keys.KeyType;

namespace LibHac.Tools.FsSystem.NcaUtils;

public class Nca(KeySet keySet, IStorage storage)
{
    private bool IsEncrypted => Header.IsEncrypted;

    private byte[] Nca0KeyArea { get; set; }
    private IStorage Nca0TransformedBody { get; set; }

    public IStorage BaseStorage { get; } = storage;

    public NcaHeader Header { get; } = new NcaHeader(keySet, storage);

    public ulong ProgramId => Header.TitleId;
    public ulong ProgramIdBase => Header.TitleId & ~0x1FFFUL;
    public int ProgramIndex => (int)(Header.TitleId & 0xF);
    public bool IsControl => Header.ContentType is NcaContentType.Control;
    public bool IsProgram => Header.ContentType is NcaContentType.Program;
    public bool IsMain => IsProgram && !IsPatch;

    public bool IsPatch
    {
        get
        {
            int dataIndex = GetSectionIndexFromType(NcaSectionType.Data, NcaContentType.Program);

            return IsProgram && SectionExists(NcaSectionType.Data) && Header.GetFsHeader(dataIndex).IsPatchSection();
        }
    } 

    public byte[] GetDecryptedKey(int index)
    {
        if (index < 0 || index > 3) throw new ArgumentOutOfRangeException(nameof(index));

        // Handle old NCA0s that use different key area encryption
        if (Header.FormatVersion == NcaVersion.Nca0FixedKey || Header.FormatVersion == NcaVersion.Nca0RsaOaep)
        {
            return GetDecryptedKeyAreaNca0().AsSpan(0x10 * index, 0x10).ToArray();
        }

        int keyRevision = Utilities.GetMasterKeyRevision(Header.KeyGeneration);
        byte[] keyAreaKey = keySet.KeyAreaKeys[keyRevision][Header.KeyAreaKeyIndex].DataRo.ToArray();

        if (keyAreaKey.IsZeros())
        {
            string keyName = $"key_area_key_{KakNames[Header.KeyAreaKeyIndex]}_{keyRevision:x2}";
            throw new MissingKeyException("Unable to decrypt NCA section.", keyName, KeyType.Common);
        }

        byte[] encryptedKey = Header.GetEncryptedKey(index).ToArray();
        byte[] decryptedKey = new byte[Aes.KeySize128];

        Aes.DecryptEcb128(encryptedKey, decryptedKey, keyAreaKey);

        return decryptedKey;
    }

    private static readonly string[] KakNames = ["application", "ocean", "system"];

    public byte[] GetEncryptedTitleKey()
    {
        RightsId rightsId = new(Header.RightsId);

        if (keySet.ExternalKeySet.Get(rightsId, out AccessKey accessKey).IsFailure())
        {
            throw new MissingKeyException("Missing NCA title key.", rightsId.ToString(), KeyType.Title);
        }
        
        return accessKey.Value.ToArray();
    }

    public byte[] GetDecryptedTitleKey()
    {
        int keyRevision = Utilities.GetMasterKeyRevision(Header.KeyGeneration);
        byte[] titleKek = keySet.TitleKeks[keyRevision].DataRo.ToArray();

        RightsId rightsId = new(Header.RightsId);

        if (keySet.ExternalKeySet.Get(rightsId, out AccessKey accessKey).IsFailure())
        {
            throw new MissingKeyException("Missing NCA title key.", rightsId.ToString(), KeyType.Title);
        }

        if (titleKek.IsZeros())
        {
            string keyName = $"titlekek_{keyRevision:x2}";
            throw new MissingKeyException("Unable to decrypt title key.", keyName, KeyType.Common);
        }

        byte[] encryptedKey = accessKey.Value.ToArray();
        byte[] decryptedKey = new byte[Aes.KeySize128];

        Aes.DecryptEcb128(encryptedKey, decryptedKey, titleKek);

        return decryptedKey;
    }

    public byte[] GetContentKey(NcaKeyType type)
    {
        return Header.HasRightsId ? GetDecryptedTitleKey() : GetDecryptedKey((int)type);
    }

    public bool CanOpenSection(NcaSectionType type)
    {
        if (!TryGetSectionIndexFromType(type, Header.ContentType, out int index))
        {
            return false;
        }

        return CanOpenSection(index);
    }

    public bool CanOpenSection(int index)
    {
        if (!SectionExists(index)) return false;
        if (GetFsHeader(index).EncryptionType == NcaEncryptionType.None) return true;

        int keyRevision = Utilities.GetMasterKeyRevision(Header.KeyGeneration);

        if (Header.HasRightsId)
        {
            return keySet.ExternalKeySet.Contains(new RightsId(Header.RightsId)) &&
                   !keySet.TitleKeks[keyRevision].IsZeros();
        }

        return !keySet.KeyAreaKeys[keyRevision][Header.KeyAreaKeyIndex].IsZeros();
    }

    public bool SectionExists(NcaSectionType type)
    {
        if (!TryGetSectionIndexFromType(type, Header.ContentType, out int index))
        {
            return false;
        }

        return SectionExists(index);
    }

    public bool SectionExists(int index)
    {
        return Header.IsSectionEnabled(index);
    }

    public NcaFsHeader GetFsHeader(int index)
    {
        if (Header.IsNca0())
            return GetNca0FsHeader(index);

        return Header.GetFsHeader(index);
    }

    private IStorage OpenSectionStorage(int index)
    {
        if (!SectionExists(index)) throw new ArgumentException(string.Format(Messages.NcaSectionMissing, index), nameof(index));

        long offset = Header.GetSectionStartOffset(index);
        long size = Header.GetSectionSize(index);

        BaseStorage.GetSize(out long ncaStorageSize).ThrowIfFailure();

        NcaFsHeader fsHeader = Header.GetFsHeader(index);

        if (fsHeader.ExistsSparseLayer())
        {
            ref NcaSparseInfo sparseInfo = ref fsHeader.GetSparseInfo();

            Unsafe.SkipInit(out BucketTree.Header header);
            sparseInfo.MetaHeader[..].CopyTo(SpanHelpers.AsByteSpan(ref header));
            header.Verify().ThrowIfFailure();

            SparseStorage sparseStorage = new();

            if (header.EntryCount == 0)
            {
                sparseStorage.Initialize(size);
            }
            else
            {
                long dataSize = sparseInfo.GetPhysicalSize();

                if (!IsSubRange(sparseInfo.PhysicalOffset, dataSize, ncaStorageSize))
                {
                    throw new InvalidDataException(
                        $"Section offset (0x{offset:x}) and length (0x{size:x}) fall outside the total NCA length (0x{ncaStorageSize:x}).");
                }

                IStorage baseStorage = BaseStorage.Slice(sparseInfo.PhysicalOffset, dataSize);
                baseStorage.GetSize(out long baseStorageSize).ThrowIfFailure();

                long metaOffset = sparseInfo.MetaOffset;
                long metaSize = sparseInfo.MetaSize;

                if (metaOffset - sparseInfo.PhysicalOffset + metaSize > baseStorageSize)
                    ResultFs.NcaBaseStorageOutOfRangeB.Value.ThrowIfFailure();

                IStorage metaStorageEncrypted = baseStorage.Slice(metaOffset, metaSize);

                ulong upperCounter = sparseInfo.MakeAesCtrUpperIv(new NcaAesCtrUpperIv(fsHeader.Counter)).Value;
                IStorage metaStorage = OpenAesCtrStorage(metaStorageEncrypted, index, sparseInfo.PhysicalOffset + metaOffset, upperCounter);

                long nodeOffset = 0;
                long nodeSize = IndirectStorage.QueryNodeStorageSize(header.EntryCount);
                // ReSharper disable once UselessBinaryOperation
                long entryOffset = nodeOffset + nodeSize;
                long entrySize = IndirectStorage.QueryEntryStorageSize(header.EntryCount);

                using ValueSubStorage nodeStorage = new(metaStorage, nodeOffset, nodeSize);
                using ValueSubStorage entryStorage = new(metaStorage, entryOffset, entrySize);

                sparseStorage.Initialize(new ArrayPoolMemoryResource(), in nodeStorage, in entryStorage, header.EntryCount).ThrowIfFailure();

                using ValueSubStorage dataStorage = new(baseStorage, 0, sparseInfo.GetPhysicalSize());
                sparseStorage.SetDataStorage(in dataStorage);
            }

            return sparseStorage;
        }

        if (!IsSubRange(offset, size, ncaStorageSize))
        {
            throw new InvalidDataException(
                $"Section offset (0x{offset:x}) and length (0x{size:x}) fall outside the total NCA length (0x{ncaStorageSize:x}).");
        }

        return BaseStorage.Slice(offset, size);
    }

    private IStorage OpenDecryptedStorage(IStorage baseStorage, int index, bool decrypting)
    {
        NcaFsHeader header = GetFsHeader(index);

        return header.EncryptionType switch
        {
            NcaEncryptionType.None => baseStorage,
            NcaEncryptionType.AesXts => OpenAesXtsStorage(baseStorage, decrypting),
            NcaEncryptionType.AesCtr => OpenAesCtrStorage(baseStorage, index, Header.GetSectionStartOffset(index), header.Counter),
            NcaEncryptionType.AesCtrEx => OpenAesCtrExStorage(baseStorage, index, decrypting),
            _ => throw new ArgumentOutOfRangeException(nameof(index), header.EncryptionType, "Unsupported encryption type."),
        };
    }

    // ReSharper disable UnusedParameter.Local
    private CachedStorage OpenAesXtsStorage(IStorage baseStorage, bool decrypting)
    {
        const int sectorSize = 0x200;

        byte[] key0 = GetContentKey(NcaKeyType.AesXts0);
        byte[] key1 = GetContentKey(NcaKeyType.AesXts1);

        // todo: Handle xts for nca version 3
        return new CachedStorage(new Aes128XtsStorage(baseStorage, key0, key1, sectorSize, true, decrypting), 2, true);
    }
    // ReSharper restore UnusedParameter.Local

    private CachedStorage OpenAesCtrStorage(IStorage baseStorage, int index, long offset, ulong upperCounter)
    {
        byte[] key = GetContentKey(NcaKeyType.AesCtr);
        byte[] counter = Aes128CtrStorage.CreateCounter(upperCounter, Header.GetSectionStartOffset(index));

        Aes128CtrStorage aesStorage = new(baseStorage, key, offset, counter, true);
        return new CachedStorage(aesStorage, 0x4000, 4, true);
    }

    private ConcatenationStorage OpenAesCtrExStorage(IStorage baseStorage, int index, bool decrypting)
    {
        NcaFsHeader fsHeader = GetFsHeader(index);
        NcaFsPatchInfo info = fsHeader.GetPatchInfo();

        long sectionOffset = Header.GetSectionStartOffset(index);
        long sectionSize = Header.GetSectionSize(index);

        long bktrOffset = info.RelocationTreeOffset;
        long bktrSize = sectionSize - bktrOffset;
        long dataSize = info.RelocationTreeOffset;

        byte[] key = GetContentKey(NcaKeyType.AesCtr);
        byte[] counter = Aes128CtrStorage.CreateCounter(fsHeader.Counter, bktrOffset + sectionOffset);
        byte[] counterEx = Aes128CtrStorage.CreateCounter(fsHeader.Counter, sectionOffset);

        IStorage bucketTreeData;
        IStorage outputBucketTreeData;

        if (decrypting)
        {
            bucketTreeData = new CachedStorage(new Aes128CtrStorage(baseStorage.Slice(bktrOffset, bktrSize), key, counter, true), 4, true);
            outputBucketTreeData = bucketTreeData;
        }
        else
        {
            bucketTreeData = baseStorage.Slice(bktrOffset, bktrSize);
            outputBucketTreeData = new CachedStorage(new Aes128CtrStorage(baseStorage.Slice(bktrOffset, bktrSize), key, counter, true), 4, true);
        }

        SubStorage encryptionBucketTreeData = new(bucketTreeData,
            info.EncryptionTreeOffset - bktrOffset, sectionSize - info.EncryptionTreeOffset);

        CachedStorage cachedBucketTreeData = new(encryptionBucketTreeData, IndirectStorage.NodeSize, 6, true);

        BucketTree.Header treeHeader = new();
        info.EncryptionTreeHeader.CopyTo(SpanHelpers.AsByteSpan(ref treeHeader));
        long nodeStorageSize = AesCtrCounterExtendedStorage.QueryNodeStorageSize(treeHeader.EntryCount);
        long entryStorageSize = AesCtrCounterExtendedStorage.QueryEntryStorageSize(treeHeader.EntryCount);

        SubStorage tableNodeStorage = new(cachedBucketTreeData, 0, nodeStorageSize);
        SubStorage tableEntryStorage = new(cachedBucketTreeData, nodeStorageSize, entryStorageSize);

        IStorage decStorage = new Aes128CtrExStorage(baseStorage.Slice(0, dataSize), tableNodeStorage,
            tableEntryStorage, treeHeader.EntryCount, key, counterEx, true);

        return new ConcatenationStorage([decStorage, outputBucketTreeData], true);
    }

    public AesCtrCounterExtendedStorage OpenAesCtrCounterExtendedStorage(int index)
    {
        if (!SectionExists(index)) return null;
        var fsHeader = GetFsHeader(index);
        if (fsHeader.EncryptionType != NcaEncryptionType.AesCtrEx) return null;

        IStorage sectionStorage = OpenSectionStorage(index);
        NcaFsPatchInfo info = fsHeader.GetPatchInfo();

        long sectionOffset = Header.GetSectionStartOffset(index);
        long sectionSize = Header.GetSectionSize(index);
        long bktrOffset = info.RelocationTreeOffset;
        long bktrSize = sectionSize - bktrOffset;

        byte[] key = GetContentKey(NcaKeyType.AesCtr);
        byte[] counter = Aes128CtrStorage.CreateCounter(fsHeader.Counter, bktrOffset + sectionOffset);

        IStorage bucketTreeData = new CachedStorage(
            new Aes128CtrStorage(sectionStorage.Slice(bktrOffset, bktrSize), key, counter, true), 4, true);

        BucketTree.Header treeHeader = new();
        info.EncryptionTreeHeader.CopyTo(SpanHelpers.AsByteSpan(ref treeHeader));
        treeHeader.Verify().ThrowIfFailure();

        long nodeStorageSize = AesCtrCounterExtendedStorage.QueryNodeStorageSize(treeHeader.EntryCount);
        long entryStorageSize = AesCtrCounterExtendedStorage.QueryEntryStorageSize(treeHeader.EntryCount);

        // 그 다음에 cachedBucketTreeData 만들고
        long encTreeRelOffset = info.EncryptionTreeOffset - bktrOffset;
        long encTreeSize = sectionSize - info.EncryptionTreeOffset;

        IStorage encryptionBucketTreeData = bucketTreeData.Slice(encTreeRelOffset, encTreeSize);
        CachedStorage cachedBucketTreeData = new(encryptionBucketTreeData, IndirectStorage.NodeSize, 6, true);

        var tableNodeStorage = new ValueSubStorage(cachedBucketTreeData, 0, nodeStorageSize);
        var tableEntryStorage = new ValueSubStorage(cachedBucketTreeData, nodeStorageSize, entryStorageSize);
        var dataStorage = new ValueSubStorage(sectionStorage, 0, info.EncryptionTreeOffset);

        using UniqueRef<AesCtrCounterExtendedStorage.IDecryptor> decryptor = new();
        AesCtrCounterExtendedStorage.CreateSoftwareDecryptor(ref decryptor.Ref).ThrowIfFailure();

        var extStorage = new AesCtrCounterExtendedStorage();

        NcaAesCtrUpperIv upperIv = new() { Value = fsHeader.Counter };

        extStorage.Initialize(new ArrayPoolMemoryResource(), key, upperIv.SecureValue,
            0, in dataStorage, in tableNodeStorage, in tableEntryStorage,
            treeHeader.EntryCount, ref decryptor.Ref).ThrowIfFailure();

        return extStorage;
    }

    public IStorage OpenRawStorage(int index, bool openEncrypted)
    {
        if (Header.IsNca0())
            return OpenNca0RawStorage(index, openEncrypted);

        IStorage storage = OpenSectionStorage(index);

        if (IsEncrypted == openEncrypted)
        {
            return storage;
        }

        IStorage decryptedStorage = OpenDecryptedStorage(storage, index, !openEncrypted);

        return decryptedStorage;
    }

    public IStorage OpenRawStorage(int index) => OpenRawStorage(index, false);

    public IStorage OpenRawStorageWithPatch(Nca patchNca, int index)
    {
        IStorage baseStorage = SectionExists(index) ? OpenRawStorage(index) : new NullStorage();
        return CreatePatchedRawStorage(patchNca, index, baseStorage);
    }

    private static IStorage CreatePatchedRawStorage(Nca patchNca, int index, IStorage baseStorage)
    {
        IStorage patchStorage = patchNca.OpenRawStorage(index);

        patchStorage.GetSize(out long patchSize).ThrowIfFailure();
        baseStorage.GetSize(out long baseSize).ThrowIfFailure();

        NcaFsHeader header = patchNca.GetFsHeader(index);
        NcaFsPatchInfo patchInfo = header.GetPatchInfo();

        if (patchInfo.RelocationTreeSize == 0)
        {
            return patchStorage;
        }

        BucketTree.Header treeHeader = new();
        patchInfo.RelocationTreeHeader.CopyTo(SpanHelpers.AsByteSpan(ref treeHeader));
        long nodeStorageSize = IndirectStorage.QueryNodeStorageSize(treeHeader.EntryCount);
        long entryStorageSize = IndirectStorage.QueryEntryStorageSize(treeHeader.EntryCount);

        SubStorage relocationTableStorage = new(patchStorage, patchInfo.RelocationTreeOffset, patchInfo.RelocationTreeSize);
        CachedStorage cachedTableStorage = new(relocationTableStorage, IndirectStorage.NodeSize, 4, true);

        using ValueSubStorage tableNodeStorage = new(cachedTableStorage, 0, nodeStorageSize);
        using ValueSubStorage tableEntryStorage = new(cachedTableStorage, nodeStorageSize, entryStorageSize);

        IndirectStorage storage = new();
        storage.Initialize(new ArrayPoolMemoryResource(), in tableNodeStorage, in tableEntryStorage, treeHeader.EntryCount).ThrowIfFailure();

        storage.SetStorage(0, baseStorage, 0, baseSize);
        storage.SetStorage(1, patchStorage, 0, patchSize);

        return storage;
    }

    public IStorage OpenStorageWithoutBase(int index, IntegrityCheckLevel integrityCheckLevel)
    {
        IStorage rawStorage = CreatePatchedRawStorage(this, index, new NullStorage());
        NcaFsHeader header = GetFsHeader(index);

        IStorage returnStorage = CreateVerificationStorage(integrityCheckLevel, header, rawStorage);

        if (header.ExistsCompressionLayer())
        {
            returnStorage = OpenCompressedStorage(header, returnStorage);
        }

        return returnStorage;
    }

    public IFileSystem OpenFileSystemWithoutBase(NcaSectionType type, IntegrityCheckLevel integrityCheckLevel)
    {
        int index = GetSectionIndexFromType(type);
        return OpenFileSystem(OpenStorageWithoutBase(index, integrityCheckLevel), GetFsHeader(index));
    }

    public IStorage OpenStorage(int index, IntegrityCheckLevel integrityCheckLevel)
    {
        return OpenStorage(index, integrityCheckLevel, false);
    }

    public IStorage OpenStorage(int index, IntegrityCheckLevel integrityCheckLevel, bool leaveCompressed)
    {
        IStorage rawStorage = OpenRawStorage(index);
        NcaFsHeader header = GetFsHeader(index);

        if (header.EncryptionType == NcaEncryptionType.AesCtrEx)
        {
            return rawStorage.Slice(0, header.GetPatchInfo().RelocationTreeOffset);
        }

        IStorage returnStorage = CreateVerificationStorage(integrityCheckLevel, header, rawStorage);

        if (!leaveCompressed && header.ExistsCompressionLayer())
        {
            returnStorage = OpenCompressedStorage(header, returnStorage);
        }

        return returnStorage;
    }

    public IStorage OpenStorageWithPatch(Nca patchNca, int index, IntegrityCheckLevel integrityCheckLevel)
    {
        return OpenStorageWithPatch(patchNca, index, integrityCheckLevel, false);
    }

    public IStorage OpenStorageWithPatch(Nca patchNca, int index, IntegrityCheckLevel integrityCheckLevel,
        bool leaveCompressed)
    {
        IStorage rawStorage = OpenRawStorageWithPatch(patchNca, index);
        NcaFsHeader header = patchNca.GetFsHeader(index);

        IStorage returnStorage = CreateVerificationStorage(integrityCheckLevel, header, rawStorage);

        if (!leaveCompressed && header.ExistsCompressionLayer())
        {
            returnStorage = OpenCompressedStorage(header, returnStorage);
        }

        return returnStorage;
    }

    private static CompressedStorage CreateCompressedStorage(NcaFsHeader header, IStorage baseStorage)
    {
        ref NcaCompressionInfo compressionInfo = ref header.GetCompressionInfo();

        Unsafe.SkipInit(out BucketTree.Header bucketTreeHeader);
        compressionInfo.TableHeader[..].CopyTo(SpanHelpers.AsByteSpan(ref bucketTreeHeader));
        bucketTreeHeader.Verify().ThrowIfFailure();

        long nodeStorageSize = CompressedStorage.QueryNodeStorageSize(bucketTreeHeader.EntryCount);
        long entryStorageSize = CompressedStorage.QueryEntryStorageSize(bucketTreeHeader.EntryCount);
        long tableOffset = compressionInfo.TableOffset;
        long tableSize = compressionInfo.TableSize;

        if (entryStorageSize + nodeStorageSize > tableSize)
            throw new HorizonResultException(ResultFs.NcaInvalidCompressionInfo.Value);

        using ValueSubStorage dataStorage = new(baseStorage, 0, tableOffset);
        using ValueSubStorage nodeStorage = new(baseStorage, tableOffset, nodeStorageSize);
        using ValueSubStorage entryStorage = new(baseStorage, tableOffset + nodeStorageSize, entryStorageSize);

        CompressedStorage compressedStorage = new();
        compressedStorage.Initialize(new ArrayPoolMemoryResource(), in dataStorage, in nodeStorage, in entryStorage,
            bucketTreeHeader.EntryCount).ThrowIfFailure();

        return compressedStorage;
    }

    private static CachedStorage OpenCompressedStorage(NcaFsHeader header, IStorage baseStorage)
    {
        return new CachedStorage(CreateCompressedStorage(header, baseStorage), 0x4000, 32, true);
    }

    public List<(long VirtualOffset, long PhysicalOffset, byte Type, sbyte Level, uint PhysicalSize)> ReadCompressionEntries(int index)
    {
        NcaFsHeader header = GetFsHeader(index);
        IStorage verified = CreateVerificationStorage(IntegrityCheckLevel.None, header, OpenRawStorage(index));

        var entries = new List<CompressedStorage.Entry>();
        CreateCompressedStorage(header, verified).GetEntries(entries).ThrowIfFailure();

        var result = new List<(long, long, byte, sbyte, uint)>();

        foreach (var e in entries)
            result.Add((e.VirtualOffset, e.PhysicalOffset, (byte)e.CompressionType, e.CompressionLevel, e.PhysicalSize));

        return result;
    }

    public (long TableOffset, long TableSize, int EntryCount) ReadCompressionTableInfo(int index)
    {
        NcaFsHeader header = GetFsHeader(index);
        ref NcaCompressionInfo info = ref header.GetCompressionInfo();

        Unsafe.SkipInit(out BucketTree.Header bucketTreeHeader);
        info.TableHeader[..].CopyTo(SpanHelpers.AsByteSpan(ref bucketTreeHeader));

        return (info.TableOffset, info.TableSize, bucketTreeHeader.EntryCount);
    }

    private HierarchicalIntegrityVerificationStorage CreateVerificationStorage(IntegrityCheckLevel integrityCheckLevel, NcaFsHeader header,
            IStorage rawStorage)
    {
        switch (header.HashType)
        {
            case NcaHashType.Sha256:
                return InitIvfcForPartitionFs(header.GetIntegrityInfoSha256(), rawStorage, integrityCheckLevel,
                    true);
            case NcaHashType.Ivfc:
                // The FS header of an NCA0 section with IVFC verification must be manually skipped
                if (Header.IsNca0())
                {
                    rawStorage = rawStorage.Slice(0x200);
                }

                return InitIvfcForRomFs(header.GetIntegrityInfoIvfc(), rawStorage, integrityCheckLevel, true);
            default:
                throw new ArgumentOutOfRangeException(nameof(header), header.HashType, "Unsupported hash type.");
        }
    }

    public IFileSystem OpenFileSystem(int index, IntegrityCheckLevel integrityCheckLevel)
    {
        IStorage storage = OpenStorage(index, integrityCheckLevel);
        NcaFsHeader header = GetFsHeader(index);

        return OpenFileSystem(storage, header);
    }

    public IFileSystem OpenFileSystemWithPatch(Nca patchNca, int index, IntegrityCheckLevel integrityCheckLevel)
    {
        IStorage storage = OpenStorageWithPatch(patchNca, index, integrityCheckLevel);
        NcaFsHeader header = patchNca.GetFsHeader(index);

        return OpenFileSystem(storage, header);
    }

    private static IFileSystem OpenFileSystem(IStorage storage, NcaFsHeader header)
    {
        switch (header.FormatType)
        {
            case NcaFormatType.Pfs0:
                PartitionFileSystem pfs = new();
                pfs.Initialize(storage).ThrowIfFailure();
                return pfs;
            case NcaFormatType.Romfs:
                return new RomFsFileSystem(storage);
            default:
                throw new ArgumentOutOfRangeException(nameof(header), header.FormatType, "Unsupported format type.");
        }
    }

    public IFileSystem OpenFileSystem(NcaSectionType type, IntegrityCheckLevel integrityCheckLevel)
    {
        return OpenFileSystem(GetSectionIndexFromType(type), integrityCheckLevel);
    }

    public IFileSystem OpenFileSystemWithPatch(Nca patchNca, NcaSectionType type, IntegrityCheckLevel integrityCheckLevel)
    {
        return OpenFileSystemWithPatch(patchNca, GetSectionIndexFromType(type), integrityCheckLevel);
    }

    public IStorage OpenRawStorage(NcaSectionType type)
    {
        return OpenRawStorage(GetSectionIndexFromType(type));
    }

    public IStorage OpenRawStorageWithPatch(Nca patchNca, NcaSectionType type)
    {
        return OpenRawStorageWithPatch(patchNca, GetSectionIndexFromType(type));
    }

    public IStorage OpenStorage(NcaSectionType type, IntegrityCheckLevel integrityCheckLevel)
    {
        return OpenStorage(GetSectionIndexFromType(type), integrityCheckLevel);
    }

    public IStorage OpenStorageWithPatch(Nca patchNca, NcaSectionType type, IntegrityCheckLevel integrityCheckLevel)
    {
        return OpenStorageWithPatch(patchNca, GetSectionIndexFromType(type), integrityCheckLevel);
    }

    public IStorage OpenEncryptedNca() => OpenFullNca(true);
    public IStorage OpenDecryptedNca() => OpenFullNca(false);

    public IStorage OpenFullNca(bool openEncrypted)
    {
        if (openEncrypted == IsEncrypted)
        {
            return BaseStorage;
        }

        ConcatenationStorageBuilder builder = new();
        builder.Add(OpenHeaderStorage(openEncrypted), 0);

        if (Header.IsNca0())
        {
            builder.Add(OpenNca0BodyStorage(openEncrypted), 0x400);
            return builder.Build();
        }

        for (int i = 0; i < NcaHeader.SectionCount; i++)
        {
            if (Header.IsSectionEnabled(i))
            {
                builder.Add(OpenRawStorage(i, openEncrypted), Header.GetSectionStartOffset(i));
            }
        }

        return builder.Build();
    }

    private int GetSectionIndexFromType(NcaSectionType type)
    {
        return GetSectionIndexFromType(type, Header.ContentType);
    }

    public static int GetSectionIndexFromType(NcaSectionType type, NcaContentType contentType)
    {
        if (!TryGetSectionIndexFromType(type, contentType, out int index))
        {
            throw new ArgumentOutOfRangeException(nameof(type), "NCA does not contain this section type.");
        }

        return index;
    }

    public static bool TryGetSectionIndexFromType(NcaSectionType type, NcaContentType contentType, out int index)
    {
        switch (type)
        {
            case NcaSectionType.Code when contentType == NcaContentType.Program:
                index = 0;
                return true;
            case NcaSectionType.Data when contentType == NcaContentType.Program:
                index = 1;
                return true;
            case NcaSectionType.Logo when contentType == NcaContentType.Program:
                index = 2;
                return true;
            case NcaSectionType.Data:
                index = 0;
                return true;
            default:
                index = 0;
                return false;
        }
    }

    public static NcaSectionType GetSectionTypeFromIndex(int index, NcaContentType contentType)
    {
        if (!TryGetSectionTypeFromIndex(index, contentType, out NcaSectionType type))
        {
            throw new ArgumentOutOfRangeException(nameof(index), "NCA type does not contain this index.");
        }

        return type;
    }

    public static bool TryGetSectionTypeFromIndex(int index, NcaContentType contentType, out NcaSectionType type)
    {
        switch (index)
        {
            case 0 when contentType == NcaContentType.Program:
                type = NcaSectionType.Code;
                return true;
            case 1 when contentType == NcaContentType.Program:
                type = NcaSectionType.Data;
                return true;
            case 2 when contentType == NcaContentType.Program:
                type = NcaSectionType.Logo;
                return true;
            case 0:
                type = NcaSectionType.Data;
                return true;
            default:
                UnsafeHelpers.SkipParamInit(out type);
                return false;
        }
    }

    private static HierarchicalIntegrityVerificationStorage InitIvfcForPartitionFs(NcaFsIntegrityInfoSha256 info,
        IStorage pfsStorage, IntegrityCheckLevel integrityCheckLevel, bool leaveOpen)
    {
        Debug.Assert(info.LevelCount == 2);

        IStorage hashStorage = pfsStorage.Slice(info.GetLevelOffset(0), info.GetLevelSize(0), leaveOpen);
        IStorage dataStorage = pfsStorage.Slice(info.GetLevelOffset(1), info.GetLevelSize(1), leaveOpen);

        IntegrityVerificationInfo[] initInfo =
        [
            // Set the master hash
            new IntegrityVerificationInfo
            {
                // todo Get hash directly from header
                Data = new MemoryStorage(info.MasterHash.ToArray()),

                BlockSize = 0,
                Type = IntegrityStorageType.PartitionFs
            },
            new IntegrityVerificationInfo
            {
                Data = hashStorage,
                BlockSize = (int)info.GetLevelSize(0),
                Type = IntegrityStorageType.PartitionFs
            },
            new IntegrityVerificationInfo
            {
                Data = dataStorage,
                BlockSize = info.BlockSize,
                Type = IntegrityStorageType.PartitionFs
            },
        ];
        return new HierarchicalIntegrityVerificationStorage(initInfo, integrityCheckLevel, leaveOpen);
    }

    private static HierarchicalIntegrityVerificationStorage InitIvfcForRomFs(NcaFsIntegrityInfoIvfc ivfc,
        IStorage dataStorage, IntegrityCheckLevel integrityCheckLevel, bool leaveOpen)
    {
        IntegrityVerificationInfo[] initInfo = new IntegrityVerificationInfo[ivfc.LevelCount];

        initInfo[0] = new IntegrityVerificationInfo
        {
            Data = new MemoryStorage(ivfc.MasterHash.ToArray()),
            BlockSize = 0
        };

        for (int i = 1; i < ivfc.LevelCount; i++)
        {
            initInfo[i] = new IntegrityVerificationInfo
            {
                Data = dataStorage.Slice(ivfc.GetLevelOffset(i - 1), ivfc.GetLevelSize(i - 1)),
                BlockSize = 1 << ivfc.GetLevelBlockSize(i - 1),
                Type = IntegrityStorageType.RomFs
            };
        }

        return new HierarchicalIntegrityVerificationStorage(initInfo, integrityCheckLevel, leaveOpen);
    }

    public IStorage OpenDecryptedHeaderStorage() => OpenHeaderStorage(false);

    public IStorage OpenHeaderStorage(bool openEncrypted)
    {
        long firstSectionOffset = long.MaxValue;
        bool hasEnabledSection = false;

        // Encrypted portion continues until the first section
        for (int i = 0; i < NcaHeader.SectionCount; i++)
        {
            if (Header.IsSectionEnabled(i))
            {
                hasEnabledSection = true;
                firstSectionOffset = Math.Min(firstSectionOffset, Header.GetSectionStartOffset(i));
            }
        }

        long headerSize = hasEnabledSection ? firstSectionOffset : NcaHeader.HeaderSize;
        IStorage rawHeaderStorage = BaseStorage.Slice(0, headerSize);

        if (openEncrypted == IsEncrypted)
            return rawHeaderStorage;

        IStorage header = Header.Version switch
        {
            3 => new CachedStorage(
                new Aes128XtsStorage(rawHeaderStorage, keySet.HeaderKey, NcaHeader.HeaderSectorSize, true,
                    !openEncrypted), 1, true),
            2 => OpenNca2Header(headerSize, !openEncrypted),
            0 => new CachedStorage(
                new Aes128XtsStorage(BaseStorage.Slice(0, 0x400), keySet.HeaderKey, NcaHeader.HeaderSectorSize, true,
                    !openEncrypted), 1, true),
            _ => throw new NotSupportedException("Unsupported NCA version")
        };

        return header;
    }

    private ConcatenationStorage OpenNca2Header(long size, bool decrypting)
    {
        const int sectorSize = NcaHeader.HeaderSectorSize;

        List<IStorage> sources =
        [
            new CachedStorage(
                new Aes128XtsStorage(BaseStorage.Slice(0, 0x400), keySet.HeaderKey, sectorSize, true, decrypting), 1, true)
        ];

        for (int i = 0x400; i < size; i += sectorSize)
        {
            sources.Add(new CachedStorage(new Aes128XtsStorage(BaseStorage.Slice(i, sectorSize), keySet.HeaderKey, sectorSize, true, decrypting), 1, true));
        }

        return new ConcatenationStorage(sources, true);
    }

    private byte[] GetDecryptedKeyAreaNca0()
    {
        if (Nca0KeyArea != null)
            return Nca0KeyArea;

        if (Header.FormatVersion == NcaVersion.Nca0FixedKey)
        {
            Nca0KeyArea = Header.GetKeyArea().ToArray();
        }
        else if (Header.FormatVersion == NcaVersion.Nca0RsaOaep)
        {
            Span<byte> keyArea = Header.GetKeyArea();
            byte[] decKeyArea = new byte[0x100];

            if (CryptoOld.DecryptRsaOaep(keyArea, decKeyArea, keySet.BetaNca0KeyAreaKeyParams, out _))
            {
                Nca0KeyArea = decKeyArea;
            }
            else
            {
                throw new InvalidDataException("Unable to decrypt NCA0 key area.");
            }
        }
        else
        {
            throw new NotSupportedException();
        }

        return Nca0KeyArea;
    }

    private IStorage OpenNca0BodyStorage(bool openEncrypted)
    {
        // NCA0 encrypts the entire NCA body using AES-XTS instead of
        // using different encryption types and IVs for each section.
        Assert.SdkEqual(0, Header.Version);

        if (openEncrypted == IsEncrypted)
        {
            return GetRawStorage();
        }

        if (Nca0TransformedBody != null)
            return Nca0TransformedBody;

        byte[] key0 = GetContentKey(NcaKeyType.AesXts0);
        byte[] key1 = GetContentKey(NcaKeyType.AesXts1);

        Nca0TransformedBody = new CachedStorage(new Aes128XtsStorage(GetRawStorage(), key0, key1, NcaHeader.HeaderSectorSize, true, !openEncrypted), 1, true);
        return Nca0TransformedBody;

        IStorage GetRawStorage()
        {
            BaseStorage.GetSize(out long ncaSize).ThrowIfFailure();
            return BaseStorage.Slice(0x400, ncaSize - 0x400);
        }
    }

    private SubStorage OpenNca0RawStorage(int index, bool openEncrypted)
    {
        if (!SectionExists(index)) throw new ArgumentException(string.Format(Messages.NcaSectionMissing, index), nameof(index));

        long offset = Header.GetSectionStartOffset(index) - 0x400;
        long size = Header.GetSectionSize(index);

        IStorage bodyStorage = OpenNca0BodyStorage(openEncrypted);

        bodyStorage.GetSize(out long baseSize).ThrowIfFailure();

        if (!IsSubRange(offset, size, baseSize))
        {
            throw new InvalidDataException(
                $"Section offset (0x{offset + 0x400:x}) and length (0x{size:x}) fall outside the total NCA length (0x{baseSize + 0x400:x}).");
        }

        return new SubStorage(bodyStorage, offset, size);
    }

    public NcaFsHeader GetNca0FsHeader(int index)
    {
        // NCA0 stores the FS header in the first block of the section instead of the header
        IStorage bodyStorage = OpenNca0BodyStorage(false);
        long offset = Header.GetSectionStartOffset(index) - 0x400;

        byte[] fsHeaderData = new byte[0x200];
        bodyStorage.Read(offset, fsHeaderData).ThrowIfFailure();

        return new NcaFsHeader(fsHeaderData);
    }

    public Validity VerifyHeaderSignature()
    {
        return Header.VerifySignature1(keySet.NcaHeaderSigningKeyParams[0].Modulus);
    }

    internal void GenerateAesCounter(int sectionIndex, LibHac.Ncm.ContentType type, int minorVersion)
    {
        NcaFsHeader header = GetFsHeader(sectionIndex);
        if (header.EncryptionType != NcaEncryptionType.AesCtr &&
            header.EncryptionType != NcaEncryptionType.AesCtrEx) return;
        var counterType = type switch
        {
            LibHac.Ncm.ContentType.Program => sectionIndex + 1,
            LibHac.Ncm.ContentType.HtmlDocument => (int)LibHac.Ncm.ContentType.HtmlDocument,
            LibHac.Ncm.ContentType.LegalInformation => (int)LibHac.Ncm.ContentType.LegalInformation,
            _ => 0,
        };
        var counterVersion = Header.ContentType switch
        {
            NcaContentType.Program or NcaContentType.Manual => Math.Max(minorVersion - 1, 0),
            NcaContentType.PublicData => minorVersion << 16,
            _ => 0,
        };
        header.CounterType = counterType;
        header.CounterVersion = counterVersion;
    }

    private static bool IsSubRange(long startIndex, long subLength, long length)
    {
        bool isOutOfRange = startIndex < 0 || startIndex > length || subLength < 0 || startIndex > length - subLength;
        return !isOutOfRange;
    }
}