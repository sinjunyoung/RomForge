namespace WiiU.WUP.Models
{
    public class ContentDetails
    {
        private bool _isContent = true;
        private bool _isEncrypted = true;
        private bool _isHashed = false;
        private short _groupID = 0x0000;
        private long _parentTitleID = 0x0;
        private short _entriesFlag = 0x0000;

        public bool IsContent { get => _isContent; set => _isContent = value; }
        public bool IsEncrypted { get => _isEncrypted; set => _isEncrypted = value; }
        public bool IsHashed { get => _isHashed; private set => _isHashed = value; }
        public short GroupID { get => _groupID; private set => _groupID = value; }
        public long ParentTitleID { get => _parentTitleID; private set => _parentTitleID = value; }
        public short EntriesFlag { get => _entriesFlag; private set => _entriesFlag = value; }

        public ContentDetails(bool isHashed, short groupID, long parentTitleID, short entriesFlags)
        {
            IsHashed = isHashed;
            GroupID = groupID;
            ParentTitleID = parentTitleID;
            EntriesFlag = entriesFlags;
        }

        public override string ToString() => "ContentDetails [isContent=" + IsContent + ", isEncrypted=" + IsEncrypted + ", isHashed=" + IsHashed + ", groupID=" + GroupID + ", parentTitleID=" + ParentTitleID + ", entriesFlag=" + EntriesFlag + "]";
    }
}