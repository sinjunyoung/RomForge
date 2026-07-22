using System;
using NUSPacker.Nuspackage.Contents;
using NUSPacker.Nuspackage.Crypto;
using NUSPacker.Nuspackage.Interfaces;
using NUSPacker.Utils;

namespace NUSPacker.Nuspackage
{
    public class TMD : IHasData
    {
        private readonly int signatureType = 0x00010004;                       // 0x000
        private readonly byte[] signature = new byte[0x100];                   // 0x004
        private readonly byte[] padding0 = new byte[0x3C];                     // 0x104
        private readonly byte[] issuer = Utils.Utils.HexStringToByteArray(
            "526F6F742D434130303030303030332D435030303030303030620000000000000000000000000000000000000000000000000000000000000000000000000000"); // 0x140

        private readonly byte version = 0x01;                                  // 0x180
        private readonly byte CACRLVersion = 0x00;                             // 0x181
        private readonly byte signerCRLVersion = 0x00;                         // 0x182
        private readonly byte padding1 = 0x00;                                 // 0x183

        private long systemVersion = 0x000500101000400AL;                      // 0x184
        // private long titleID = 0x0000000000000000L;                        // 0x18C Info the from the ticket will be used
        private readonly int titleType = 0x000100;                             // 0x194
        private short groupID = 0x0000;                                        // 0x198
        private int appType = unchecked((int)0x80000000); // for updates 0x0800001B;           // 0x19A
        private readonly int random1 = 0;
        private readonly int random2 = 0; // 0x02FE6000; //something about the (encrypted) sizes?
        private readonly byte[] reserved = new byte[50];
        private readonly int accessRights = 0x0000;                            // 0x1D8
        private short titleVersion = 0x00;                                     // 0x1DC
        private short contentCount = 0x00;                                     // 0x1DE
        private readonly short bootIndex = 0x00;                               // 0x1E0
        private readonly byte[] padding3 = new byte[2];                        // 0x1E2
        private byte[] SHA2 = new byte[0x20];                                  // 0x1E4

        private ContentInfos? contentInfos = null;
        private Contents.Contents? contents = null;
        // private byte[] certs = Cert.getTMDCertAsData();                     // not needed

        private Ticket ticket = null!;

        public TMD(AppXMLInfo appInfo, FST fst, Ticket ticket)
        {
            SetGroupID(appInfo.GetGroupID());
            SetSystemVersion(appInfo.GetOSVersion());
            SetAppType(appInfo.GetAppType());
            SetTitleVersion(appInfo.GetTitleVersion());
            SetTicket(ticket);
            SetContents(fst.GetContents());
            contentInfos = new ContentInfos();
        }

        private void SetContents(Contents.Contents contents)
        {
            if (contents != null)
            {
                this.contents = contents;
                contentCount = contents.GetContentCount();
            }
        }

        public void Update()
        {
            UpdateContents();
        }

        public void UpdateContents()
        {
            contentCount = contents!.GetContentCount();

            ContentInfo firstContentInfo = new ContentInfo(contents.GetContentCount());

            firstContentInfo.SetSHA2Hash(HashUtil.HashSHA2(contents.GetAsData()));
            GetContentInfos().SetContentInfo(0, firstContentInfo);
        }

        public void UpdateContentInfoHash()
        {
            SHA2 = HashUtil.HashSHA2(GetContentInfos().GetAsData());
        }

        public byte[] GetAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(GetDataSize());
            buffer.PutInt(signatureType);
            buffer.Put(signature);
            buffer.Put(padding0);
            buffer.Put(issuer);

            buffer.Put(version);
            buffer.Put(CACRLVersion);
            buffer.Put(signerCRLVersion);
            buffer.Put(padding1);

            buffer.PutLong(GetSystemVersion());
            buffer.PutLong(GetTicket().GetTitleID());
            buffer.PutInt(titleType);
            buffer.PutShort(GetGroupID());
            buffer.PutInt(GetAppType());
            buffer.PutInt(random1);
            buffer.PutInt(random2);
            buffer.Put(reserved);
            buffer.PutInt(accessRights);
            buffer.PutShort(GetTitleVersion());
            buffer.PutShort(contentCount);
            buffer.PutShort(bootIndex);

            buffer.Put(padding3);
            buffer.Put(SHA2);

            buffer.Put(GetContentInfos().GetAsData());
            buffer.Put(GetContents().GetAsData());
            // buffer.put(certs); not needed
            return buffer.Array();
        }

        public int GetDataSize()
        {
            int staticSize = 0x204;
            int contentInfoSize = contentInfos!.GetDataSize();
            int contentsSize = contents!.GetDataSize();
            // int certSize = certs.length;
            return staticSize + contentInfoSize + contentsSize; // + certSize;
        }

        public ContentInfos GetContentInfos()
        {
            return contentInfos ??= new ContentInfos();
        }

        public void SetContentInfos(ContentInfos contentInfos)
        {
            this.contentInfos = contentInfos;
        }

        public Contents.Contents GetContents()
        {
            return contents ??= new Contents.Contents();
        }

        public Ticket GetTicket() => ticket;

        public void SetTicket(Ticket ticket)
        {
            this.ticket = ticket;
        }

        public Encryption GetEncryption()
        {
            JByteBuffer iv = JByteBuffer.Allocate(0x10);
            iv.PutLong(GetTicket().GetTitleID());
            Key key = GetTicket().GetDecryptedKey();
            return new Encryption(key, new IV(iv.Array()));
        }

        public long GetSystemVersion() => systemVersion;

        public void SetSystemVersion(long systemVersion)
        {
            this.systemVersion = systemVersion;
        }

        public short GetGroupID() => groupID;

        public void SetGroupID(short groupID)
        {
            this.groupID = groupID;
        }

        public int GetAppType() => appType;

        public void SetAppType(int appType)
        {
            this.appType = appType;
        }

        public short GetTitleVersion() => titleVersion;

        public void SetTitleVersion(short titleVersion)
        {
            this.titleVersion = titleVersion;
        }
    }
}
