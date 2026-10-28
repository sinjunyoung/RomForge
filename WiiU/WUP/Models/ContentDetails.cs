namespace WiiU.WUP.Models
{
    public class ContentDetails(bool isHashed, short groupID, long parentTitleID, short entriesFlags)
    {
        public bool IsContent { get; set; } = true;

        public bool IsEncrypted { get; set; } = true;

        public bool IsHashed { get; private set; } = isHashed;

        public short GroupID { get; private set; } = groupID;

        public long ParentTitleID { get; private set; } = parentTitleID;

        public short EntriesFlag { get; private set; } = entriesFlags;

        public override string ToString() => "ContentDetails [isContent=" + IsContent + ", isEncrypted=" + IsEncrypted + ", isHashed=" + IsHashed + ", groupID=" + GroupID + ", parentTitleID=" + ParentTitleID + ", entriesFlag=" + EntriesFlag + "]";
    }
}