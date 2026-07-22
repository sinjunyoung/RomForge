using System.Diagnostics;
using WiiU.Core.WUP.NusPackage.Interfaces;
using WiiU.Core.WUP.Services;

namespace WiiU.Core.WUP.Models
{
    public class FSTEntry : IHasData, ICloneable
    {
        public enum Types : byte
        {
            DIR = 0x01,
            notInNUS = 0x80,
            WiiVC = 0x02,
        }

        public enum Flags : short
        {
            NOBIGFILE = 0x04,
            HASHED = 0x400,
        }

        private readonly string? file;

        private string filename = "";
        private FSTEntry? parent = null;
        private List<FSTEntry>? children = null;
        private int nameOffset = 0;
        private int entryOffset = 0x00;

        private short flags;

        private bool isDirFlag = false;
        private int parentOffset = 0;
        private int nextOffset = 0;

        private long filesize = 0;
        private long fileoffset = 0;

        private bool isRootFlag = false;

        private int root_entryCount = 0;

        private Content? content = null;

        private byte[]? decryptedSHA1 = new byte[0x14];

        private bool bigFile = false;

        private bool hashedFile = false;

        private bool notInPackage = false;

        private readonly Func<Stream>? streamFactory = null;

        public FSTEntry(string filePath) : this(filePath, false)
        {
        }

        public FSTEntry(string filePath, bool notInPackage)
        {
            bool isDirectory = Directory.Exists(filePath);
            bool isFileOnDisk = File.Exists(filePath);

            if (filePath == null || (!isDirectory && !isFileOnDisk))
                throw new ArgumentException("Couldn't create FSTEntry, file is NULL or doesn't exist");

            file = Path.GetFullPath(filePath);

            SetDir(isDirectory);
            SetFileName(isDirectory ? new DirectoryInfo(filePath).Name : Path.GetFileName(filePath));
            SetFileSize(isDirectory ? 0 : new FileInfo(filePath).Length);
            SetNotInPackage(notInPackage);

            if (IsFile())
                decryptedSHA1 = null;
        }

        public FSTEntry(string name, Func<Stream> openRead, long length)
        {
            file = null;
            streamFactory = openRead ?? throw new ArgumentNullException(nameof(openRead));

            SetDir(false);
            SetFileName(name);
            SetFileSize(length);
            SetNotInPackage(false);

            decryptedSHA1 = null;
        }

        public Func<Stream>? GetStreamFactory() => streamFactory;

        public FSTEntry(bool root)
        {
            file = null;

            if (root)
            {
                SetIsRoot(true);
                SetDir(true);
            }
        }

        public void AddChildren(FSTEntry fstEntry)
        {
            GetChildren().Add(fstEntry);
            fstEntry.SetParent(this);
        }

        public bool IsNotInPackage() => notInPackage;

        public void SetNotInPackage(bool notInPackage)
        {
            this.notInPackage = notInPackage;
        }

        public FSTEntry? GetParent() => parent;

        private void SetParent(FSTEntry child)
        {
            parent = child;
        }

        public Content? GetContent() => content;

        public void SetContent(Content content)
        {
            SetFlags(content.GetEntriesFlags());
            this.content = content;
        }

        public void SetContentRecursive(Content content)
        {
            SetContent(content);

            foreach (FSTEntry entry in GetChildren())
                entry.SetContentRecursive(content);
        }

        public string? GetFile() => file;

        public string GetFilename() => filename;

        public void SetFileName(string filename)
        {
            this.filename = filename;
        }

        public long GetFilesize()
        {
            if (!IsFile())
                return 0;

            return filesize;
        }

        public void SetFileSize(long filesize)
        {
            this.filesize = filesize;
        }

        public long GetFileOffset() => fileoffset;

        public void SetFileOffset(long fileOffset)
        {
            fileoffset = fileOffset;
        }

        private void SetIsRoot(bool isRoot)
        {
            isRootFlag = isRoot;
        }

        public bool IsRoot() => isRootFlag;

        public void SetDir(bool isDir)
        {
            isDirFlag = isDir;
        }

        public bool IsDir() => isDirFlag;

        public bool IsFile() => !(IsDir() || IsNotInPackage());

        public bool IsBigFile() => bigFile;

        public void SetBigFile(bool bigFile)
        {
            this.bigFile = bigFile;
        }

        public bool IsHashedFile() => hashedFile;

        public void SetHashedFile(bool hashedFile)
        {
            this.hashedFile = hashedFile;
        }

        public byte GetEntryType()
        {
            byte type = 0;

            if (IsDir()) 
                type |= (byte)Types.DIR;

            if (IsNotInPackage()) 
                type |= (byte)Types.notInNUS;

            if (GetFilename().EndsWith("nfs"))
                type |= (byte)Types.WiiVC;

            return type;
        }

        public short GetFlags() => flags;

        public FSTEntry? GetEntryByName(string name)
        {
            foreach (FSTEntry f in GetChildren())
                if (f.GetFilename().Equals(name))
                    return f;

            return null;
        }

        public byte[] GetAsData()
        {
            JByteBuffer buffer = JByteBuffer.Allocate(GetDataSize());

            if (IsRoot())
            {
                buffer.Put((byte)1);
                buffer.Put(new byte[0x07]);
                buffer.PutInt(root_entryCount);
                buffer.Put(new byte[0x04]);
            }
            else
            {
                buffer.Put(GetEntryType());
                buffer.Put((byte)((nameOffset >> 16) & 0xFF));
                buffer.PutShort((short)(nameOffset & 0xFFFF));

                if (IsDir())
                {
                    buffer.PutInt(parentOffset);
                    buffer.PutInt(nextOffset);
                }
                else if (IsFile())
                {
                    buffer.PutInt((int)(fileoffset >> 5));
                    buffer.PutInt((int)filesize);
                }
                else if (IsNotInPackage())
                {
                    buffer.PutInt(0);
                    buffer.PutInt((int)filesize);
                }

                buffer.PutShort(GetFlags());
                buffer.PutShort((short)content!.GetID());
            }

            if (children != null)
            {
                foreach (FSTEntry entry in GetChildren())
                    buffer.Put(entry.GetAsData());
            }

            return buffer.Array();
        }

        public int GetDataSize()
        {
            int size = 0x10;

            foreach (FSTEntry entry in GetChildren())
                size += entry.GetDataSize();

            return size;
        }

        public byte[] GetDecryptedHash()
        {
            if (decryptedSHA1 == null)
                CalculateDecryptedHash();

            return decryptedSHA1!;
        }

        public void SetNameOffset(int offset)
        {
            if (offset > 0xFFFFFF)
                Debug.WriteLine("Warning, filename offset is too big. Maximum is " + 0xFFFFFF + " tried to set to" + offset);

            nameOffset = offset;
        }

        public void Update()
        {
            SetNameOffset(FST.GetStringPos());
            FST.AddString(filename);
            SetEntryOffset(FST.CurEntryOffset);

            FST.CurEntryOffset++;

            if (IsDir() && !IsRoot())
                SetParentOffset(GetParent()!.GetEntryOffset());

            if (GetContent() != null && IsFile())
            {
                long fileoffset_ = GetContent()!.GetOffsetForFileAndIncrease(this);
                SetFileOffset(fileoffset_);
            }

            foreach (FSTEntry entry in GetChildren())
                entry.Update();
        }

        public FSTEntry? UpdateDirRefs()
        {
            if (!(IsDir() || IsRoot()))
                return null;

            if (parent != null)
                SetParentOffset(GetParent()!.GetEntryOffset());

            FSTEntry? result = null;

            var dirChildren = GetDirChildren();

            for (int i = 0; i < dirChildren.Count; i++)
            {
                FSTEntry cur_dir = dirChildren[i];

                if (i + 1 < dirChildren.Count)
                    cur_dir.SetNextOffset(dirChildren[i + 1].entryOffset);

                FSTEntry? cur_result = cur_dir.UpdateDirRefs();

                if (cur_result != null)
                {
                    FSTEntry cur_foo = cur_result.GetParent()!;

                    while (cur_foo.GetNextOffset() == 0)
                        cur_foo = cur_foo.GetParent()!;

                    cur_result.SetNextOffset(cur_foo.GetNextOffset());
                }

                if (!(i + 1 < dirChildren.Count))
                    result = cur_dir;
            }

            return result;
        }

        private int GetNextOffset() => nextOffset;

        public void SetEntryOffset(int entryOffset)
        {
            this.entryOffset = entryOffset;
        }

        public int GetEntryOffset() => entryOffset;

        public void SetNextOffset(int nextOffset)
        {
            this.nextOffset = nextOffset;
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();

            if (IsDir()) 
                sb.Append("DIR: ").Append('\n');

            if (IsDir()) 
                sb.Append("Filename: ").Append(GetFilename()).Append('\n');

            if (IsDir()) 
                sb.Append("       ID:").Append(GetEntryOffset()).Append('\n');

            if (IsDir()) 
                sb.Append(" ParentID:").Append(parentOffset).Append('\n');

            if (IsDir()) 
                sb.Append("   NextID:").Append(nextOffset).Append('\n');

            foreach (FSTEntry e in GetChildren())
                sb.Append(e.ToString());

            return sb.ToString();
        }

        public void PrintRecursive(int space)
        {
            if (IsNotInPackage())
                Debug.Write(" (not in package)");

            foreach (FSTEntry child in GetDirChildren(true))
                child.PrintRecursive(space + 1);

            foreach (FSTEntry child in GetFileChildren(true))
                child.PrintRecursive(space + 1);
        }

        public List<FSTEntry> GetFSTEntriesByContent(Content content)
        {
            List<FSTEntry> entries = [];

            if (this.content == null)
            {
                if (isDirFlag)
                    Debug.WriteLine("The folder \"" + GetFilename() + "\" is emtpy. Please add a dummy file to it.");
                else
                {
                    Debug.WriteLine("The file \"" + GetFilename() + "\" is not assigned to any content (.app).");
                    Debug.WriteLine("Please delete it or write a corresponding content rule");
                }

                Environment.Exit(0);
            }
            else
            {
                if (this.content.Equals(content))
                    entries.Add(this);
            }

            foreach (FSTEntry child in GetChildren())
                entries.AddRange(child.GetFSTEntriesByContent(content));

            return entries;
        }

        public List<FSTEntry> GetChildren()
        {
            return children ??= [];
        }

        public int GetEntryCount()
        {
            int count = 1;

            foreach (FSTEntry entry in GetChildren())
                count += entry.GetEntryCount();

            return count;
        }

        public void SetParentOffset(int i)
        {
            parentOffset = i;
        }

        public void SetEntryCount(int fstEntryCount)
        {
            root_entryCount = fstEntryCount;
        }

        public List<FSTEntry> GetDirChildren() => GetDirChildren(false);

        public List<FSTEntry> GetDirChildren(bool all)
        {
            List<FSTEntry> result = [];

            foreach (FSTEntry child in GetChildren())
            {
                if (child.IsDir() && (all || !child.IsNotInPackage()))
                    result.Add(child);
            }

            return result;
        }

        public List<FSTEntry> GetFileChildren() => GetFileChildren(false);

        public List<FSTEntry> GetFileChildren(bool all)
        {
            List<FSTEntry> result = [];

            foreach (FSTEntry child in GetChildren())
            {
                if (child.IsFile() || (all && !child.IsDir()))
                    result.Add(child);
            }

            return result;
        }

        public void CalculateDecryptedHash()
        {
            decryptedSHA1 = HashUtil.HashSHA1(file!, 0x8000);
        }

        public void SetFlags(short flags)
        {
            this.flags = flags;
        }

        public FSTEntry Clone()
        {
            return (FSTEntry)MemberwiseClone();
        }

        object ICloneable.Clone() => Clone();
    }
}