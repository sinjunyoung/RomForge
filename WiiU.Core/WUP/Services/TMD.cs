using WiiU.Core.WUP.Crypto;
using WiiU.Core.WUP.NusPackage.Interfaces;

namespace WiiU.Core.WUP.Services
{
    public class TMD : IHasData
    {
        private readonly int signatureType = 0x00010004;
        private readonly byte[] signature = new byte[0x100];
        private readonly byte[] padding0 = new byte[0x3C];
        private readonly byte[] issuer = Services.Utils.HexStringToByteArray("526F6F742D434130303030303030332D435030303030303030620000000000000000000000000000000000000000000000000000000000000000000000000000");

        private readonly byte version = 0x01;
        private readonly byte CACRLVersion = 0x00;
        private readonly byte signerCRLVersion = 0x00;
        private readonly byte padding1 = 0x00;

        private long systemVersion = 0x000500101000400AL;        
        private readonly int titleType = 0x000100;
        private short groupID = 0x0000;
        private int appType = unchecked((int)0x80000000);
        private readonly int random1 = 0;
        private readonly int random2 = 0;
        private readonly byte[] reserved = new byte[50];
        private readonly int accessRights = 0x0000;
        private short titleVersion = 0x00;
        private short contentCount = 0x00;
        private readonly short bootIndex = 0x00;
        private readonly byte[] padding3 = new byte[2];
        private byte[] SHA2 = new byte[0x20];

        private ContentInfos? contentInfos = null;
        private Contents? contents = null;
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

        private void SetContents(Contents contents)
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

            ContentInfo firstContentInfo = new (contents.GetContentCount());

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

            return buffer.Array();
        }

        public int GetDataSize()
        {
            int staticSize = 0x204;
            int contentInfoSize = contentInfos!.GetDataSize();
            int contentsSize = contents!.GetDataSize();

            return staticSize + contentInfoSize + contentsSize;
        }

        public ContentInfos GetContentInfos()
        {
            return contentInfos ??= new ContentInfos();
        }

        public void SetContentInfos(ContentInfos contentInfos)
        {
            this.contentInfos = contentInfos;
        }

        public Contents GetContents()
        {
            return contents ??= new Contents();
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