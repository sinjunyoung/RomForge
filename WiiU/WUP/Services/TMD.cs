using WiiU.WUP.Crypto;
using WiiU.WUP.Interfaces;
using WiiU.WUP.Models;

namespace WiiU.WUP.Services
{
    public class TMD : IHasData
    {
        private readonly int _signatureType = 0x00010004;
        private readonly byte[] _signature = new byte[0x100];
        private readonly byte[] _padding0 = new byte[0x3C];
        private readonly byte[] _issuer = Utils.HexStringToByteArray("526F6F742D434130303030303030332D435030303030303030620000000000000000000000000000000000000000000000000000000000000000000000000000");
        private readonly byte _version = 0x01;
        private readonly byte _caCRLVersion = 0x00;
        private readonly byte _signerCRLVersion = 0x00;
        private readonly byte _padding1 = 0x00;
        private readonly int _titleType = 0x000100;
        private readonly int _random1 = 0;
        private readonly int _random2 = 0;
        private readonly byte[] _reserved = new byte[50];
        private readonly int _accessRights = 0x0000;
        private readonly short _bootIndex = 0x00;
        private readonly byte[] _padding3 = new byte[2];

        private short _contentCount = 0x00;
        private byte[] _sha2 = new byte[0x20];

        public TMD(AppXMLInfo appInfo, FST fst, Ticket ticket)
        {
            GroupID = appInfo.GroupID;
            SystemVersion = appInfo.OSVersion;
            AppType = appInfo.AppType;
            TitleVersion = appInfo.TitleVersion;
            Ticket = ticket;
            Contents = fst.Contents;
        }

        public long SystemVersion { get; set; } = 0x000500101000400AL;

        public short GroupID { get; set; } = 0x0000;

        public int AppType { get; set; } = unchecked((int)0x80000000);

        public short TitleVersion { get; set; } = 0x00;

        public Ticket Ticket { get; set; } = null!;

        public ContentInfos ContentInfos { get;  set; } = new ContentInfos();

        public Contents Contents
        {
            get;
            set
            {
                field = value;

                if (value != null)
                    _contentCount = value.ContentCount;
            }
        } = new Contents();

        public Encryption Encryption
        {
            get
            {
                JByteBuffer iv = JByteBuffer.Allocate(0x10);

                iv.PutLong(Ticket.TitleID);

                Key key = Ticket.DecryptedKey;

                return new Encryption(key, new IV(iv.Array()));
            }
        }

        public void UpdateContents()
        {
            _contentCount = Contents.ContentCount;

            ContentInfo firstContentInfo = new(Contents.ContentCount)
            {
                SHA2Hash = HashUtil.HashSHA2(Contents.GetAsData())
            };

            ContentInfos[0] = firstContentInfo;
        }

        public void UpdateContentInfoHash() => _sha2 = HashUtil.HashSHA2(ContentInfos.GetAsData());

        public byte[] GetAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(GetDataSize());

            buffer.PutInt(_signatureType);
            buffer.Put(_signature);
            buffer.Put(_padding0);
            buffer.Put(_issuer);

            buffer.Put(_version);
            buffer.Put(_caCRLVersion);
            buffer.Put(_signerCRLVersion);
            buffer.Put(_padding1);

            buffer.PutLong(SystemVersion);
            buffer.PutLong(Ticket.TitleID);
            buffer.PutInt(_titleType);
            buffer.PutShort(GroupID);
            buffer.PutInt(AppType);
            buffer.PutInt(_random1);
            buffer.PutInt(_random2);
            buffer.Put(_reserved);
            buffer.PutInt(_accessRights);
            buffer.PutShort(TitleVersion);
            buffer.PutShort(_contentCount);
            buffer.PutShort(_bootIndex);

            buffer.Put(_padding3);
            buffer.Put(_sha2);

            buffer.Put(ContentInfos.GetAsData());
            buffer.Put(Contents.GetAsData());

            return buffer.Array();
        }

        public int GetDataSize()
        {
            int staticSize = 0x204;
            int contentInfoSize = ContentInfos.GetDataSize();
            int contentsSize = Contents.GetDataSize();

            return staticSize + contentInfoSize + contentsSize;
        }
    }
}