using System.Diagnostics;
using WiiU.WUP.Interfaces;
using WiiU.WUP.Services;

namespace WiiU.WUP.Models
{
    public class FSTEntry : IHasData, ICloneable
    {
        private readonly Func<Stream>? _streamFactory; 

        public Func<Stream>? StreamFactory => _streamFactory;

        public string? FilePath { get; }

        public string Filename { get; set; } = string.Empty;

        public FSTEntry? Parent { get; private set; } = null;

        public int NameOffset { get; private set; } = 0;

        public int EntryOffset { get; set; } = 0x00;

        public short Flags { get; set; }

        public bool IsDir { get; set; } = false;

        public int ParentOffset { get; set; } = 0;

        public int NextOffset { get; set; } = 0;
        
        public long Filesize
        {
            get => IsFile ? field : 0;
            set;
        }

        public long FileOffset { get; set; } = 0;

        public bool IsRoot { get; private set; } = false;

        public int RootEntryCount { get; set; } = 0;

        public Content? Content
        {
            get;
            set
            {
                field = value;

                if (value != null)
                    Flags = value.EntriesFlags;
            }
        }

        public bool IsNotInPackage { get; set; } = false;

        public bool IsFile => !(IsDir || IsNotInPackage);

        public List<FSTEntry> Children => field ??= [];

        public FSTEntry(string filePath) : this(filePath, false) { }

        public FSTEntry(string filePath, bool notInPackage)
        {
            bool isDirectory = Directory.Exists(filePath);
            bool isFileOnDisk = File.Exists(filePath);

            if (filePath == null || (!isDirectory && !isFileOnDisk))
                throw new ArgumentException("Couldn't create FSTEntry, file is NULL or doesn't exist");

            FilePath = Path.GetFullPath(filePath);

            IsDir = isDirectory;
            Filename = isDirectory ? new DirectoryInfo(filePath).Name : Path.GetFileName(filePath);
            Filesize = isDirectory ? 0 : new FileInfo(filePath).Length;
            IsNotInPackage = notInPackage;
        }

        public FSTEntry(string name, Func<Stream> openRead, long length)
        {
            FilePath = null;
            _streamFactory = openRead ?? throw new ArgumentNullException(nameof(openRead));
            IsDir = false;
            Filename = name;
            Filesize = length;
            IsNotInPackage = false;
        }

        public FSTEntry(bool root)
        {
            FilePath = null;

            if (root)
            {
                IsRoot = true;
                IsDir = true;
            }
        }

        public void AddChildren(FSTEntry fstEntry)
        {
            Children.Add(fstEntry);
            fstEntry.Parent = this;
        }

        public void SetContentRecursive(Content content)
        {
            Content = content;

            foreach (FSTEntry entry in Children)
                entry.SetContentRecursive(content);
        }

        public byte GetEntryType()
        {
            byte type = 0;

            if (IsDir)
                type |= (byte)Types.DIR;

            if (IsNotInPackage)
                type |= (byte)Types.notInNUS;

            if (Filename.EndsWith("nfs"))
                type |= (byte)Types.WiiVC;

            return type;
        }

        public byte[] GetAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(GetDataSize());

            if (IsRoot)
            {
                buffer.Put((byte)1);
                buffer.Put(new byte[0x07]);
                buffer.PutInt(RootEntryCount);
                buffer.Put(new byte[0x04]);
            }
            else
            {
                buffer.Put(GetEntryType());
                buffer.Put((byte)((NameOffset >> 16) & 0xFF));
                buffer.PutShort((short)(NameOffset & 0xFFFF));

                if (IsDir)
                {
                    buffer.PutInt(ParentOffset);
                    buffer.PutInt(NextOffset);
                }
                else if (IsFile)
                {
                    buffer.PutInt((int)(FileOffset >> 5));
                    buffer.PutInt((int)Filesize);
                }
                else if (IsNotInPackage)
                {
                    buffer.PutInt(0);
                    buffer.PutInt((int)Filesize);
                }

                buffer.PutShort(Flags);
                buffer.PutShort((short)Content!.ID);
            }

            foreach (FSTEntry entry in Children)
                buffer.Put(entry.GetAsData());

            return buffer.Array();
        }

        public int GetDataSize()
        {
            int size = 0x10;

            foreach (FSTEntry entry in Children)
                size += entry.GetDataSize();

            return size;
        }

        public void SetNameOffset(int offset)
        {
            if (offset > 0xFFFFFF)
                Debug.WriteLine("Warning, filename offset is too big. Maximum is " + 0xFFFFFF + " tried to set to" + offset);

            NameOffset = offset;
        }

        public void Update()
        {
            SetNameOffset(FST.GetStringPos());
            FST.AddString(Filename);
            EntryOffset = FST.CurEntryOffset;

            FST.CurEntryOffset++;

            if (IsDir && !IsRoot)
                ParentOffset = Parent!.EntryOffset;

            if (Content != null && IsFile)
            {
                long fileoffset_ = Content.GetOffsetForFileAndIncrease(this);
                FileOffset = fileoffset_;
            }

            foreach (FSTEntry entry in Children)
                entry.Update();
        }

        public FSTEntry? UpdateDirRefs()
        {
            if (!(IsDir || IsRoot))
                return null;

            if (Parent != null)
                ParentOffset = Parent.EntryOffset;

            FSTEntry? result = null;

            var dirChildren = GetDirChildren();

            for (int i = 0; i < dirChildren.Count; i++)
            {
                FSTEntry cur_dir = dirChildren[i];

                if (i + 1 < dirChildren.Count)
                    cur_dir.NextOffset = dirChildren[i + 1].EntryOffset;

                FSTEntry? cur_result = cur_dir.UpdateDirRefs();

                if (cur_result != null)
                {
                    FSTEntry cur_foo = cur_result.Parent!;

                    while (cur_foo.NextOffset == 0)
                        cur_foo = cur_foo.Parent!;

                    cur_result.NextOffset = cur_foo.NextOffset;
                }

                if (!(i + 1 < dirChildren.Count))
                    result = cur_dir;
            }

            return result;
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();

            if (IsDir)
                sb.Append("DIR: ").Append('\n');

            if (IsDir)
                sb.Append("Filename: ").Append(Filename).Append('\n');

            if (IsDir)
                sb.Append("       ID:").Append(EntryOffset).Append('\n');

            if (IsDir)
                sb.Append(" ParentID:").Append(ParentOffset).Append('\n');

            if (IsDir)
                sb.Append("   NextID:").Append(NextOffset).Append('\n');

            foreach (FSTEntry e in Children)
                sb.Append(e.ToString());

            return sb.ToString();
        }

        public void PrintRecursive(int space)
        {
            if (IsNotInPackage)
                Debug.Write(" (not in package)");

            foreach (FSTEntry child in GetDirChildren(true))
                child.PrintRecursive(space + 1);

            foreach (FSTEntry child in GetFileChildren(true))
                child.PrintRecursive(space + 1);
        }

        public List<FSTEntry> GetFSTEntriesByContent(Content content)
        {
            List<FSTEntry> entries = [];

            if (Content == null)
            {
                if (IsDir)
                    Debug.WriteLine("The folder \"" + Filename + "\" is emtpy. Please add a dummy file to it.");
                else
                {
                    Debug.WriteLine("The file \"" + Filename + "\" is not assigned to any content (.app).");
                    Debug.WriteLine("Please delete it or write a corresponding content rule");
                }

                Environment.Exit(0);
            }
            else
            {
                if (Content.Equals(content))
                    entries.Add(this);
            }

            foreach (FSTEntry child in Children)
                entries.AddRange(child.GetFSTEntriesByContent(content));

            return entries;
        }

        public int GetEntryCount()
        {
            int count = 1;

            foreach (FSTEntry entry in Children)
                count += entry.GetEntryCount();

            return count;
        }

        public List<FSTEntry> GetDirChildren() => GetDirChildren(false);

        public List<FSTEntry> GetDirChildren(bool all)
        {
            List<FSTEntry> result = [];

            foreach (FSTEntry child in Children)
            {
                if (child.IsDir && (all || !child.IsNotInPackage))
                    result.Add(child);
            }

            return result;
        }

        public List<FSTEntry> GetFileChildren(bool all)
        {
            List<FSTEntry> result = [];

            foreach (FSTEntry child in Children)
            {
                if (child.IsFile || (all && !child.IsDir))
                    result.Add(child);
            }

            return result;
        }

        public FSTEntry Clone() => (FSTEntry)MemberwiseClone();

        object ICloneable.Clone() => Clone();
    }
}