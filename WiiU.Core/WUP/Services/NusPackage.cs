using System.Diagnostics;
using WiiU.Core.WUP.Crypto;

namespace WiiU.Core.WUP.Services
{
    public class NUSPackage
    {
        private Ticket ticket = null!;
        private TMD tmd = null!;
        private FST fst = null!;

        private string outputdir = "output";

        public Ticket GetTicket() => ticket;

        public void SetTicket(Ticket ticket)
        {
            this.ticket = ticket;
        }

        public string GetOutputdir() => outputdir;

        public void SetOutputdir(string outputdir)
        {
            this.outputdir = outputdir;
        }

        public TMD GetTMD() => tmd;

        public void SetTMD(TMD tmd)
        {
            this.tmd = tmd;
        }

        public FST GetFST() => fst;

        public void SetFST(FST fst)
        {
            this.fst = fst;
        }

        public Contents GetContents() => GetFST().GetContents();

        public ContentInfos GetContentInfos() => GetTMD().GetContentInfos();

        public void PackContents(string outputDir)
        {
            PackContents(outputDir, null);
        }

        public void PackContents(string outputDir, Action<Content>? onContentPacked)
        {
            PackContents(outputDir, onContentPacked, null);
        }

        public void PackContents(string outputDir, Action<Content>? onContentPacked, Action<Content, string, long, long>? onContentBytesProcessed)
        {
            if (outputDir != null && outputDir.Length != 0)
                SetOutputdir(outputDir);

            try
            {
                GetFST().GetContents().PackContents(outputDir!, onContentPacked, onContentBytesProcessed);
            }
            catch (IOException e1)
            {
                Console.Error.WriteLine(e1);
            }

            Content fstContent = GetContents().GetFSTContent();

            fstContent.SetHash(HashUtil.HashSHA1(GetFST().GetAsData()));
            fstContent.SetEncryptedFileSize(GetFST().GetAsData().Length);

            ContentInfo contentInfo = GetContentInfos().GetContentInfo(0);

            contentInfo.SetSHA2Hash(HashUtil.HashSHA2(GetContents().GetAsData()));
            GetTMD().UpdateContentInfoHash();

            try
            {
                using (FileStream fos = new (GetOutputdir() + "/title.tmd", FileMode.Create, FileAccess.Write))
                {
                    byte[] data = tmd.GetAsData();
                    fos.Write(data, 0, data.Length);
                }

                using (FileStream fos = new (GetOutputdir() + "/title.cert", FileMode.Create, FileAccess.Write))
                {
                    byte[] data = Cert.GetCertAsData();
                    fos.Write(data, 0, data.Length);
                }

                using (FileStream fos = new (GetOutputdir() + "/title.tik", FileMode.Create, FileAccess.Write))
                {
                    byte[] data = ticket.GetAsData();
                    fos.Write(data, 0, data.Length);
                }
            }
            catch (IOException e)
            {
                Debug.WriteLine(e);
            }
        }

        public Encryption GetEncryption() => GetTMD().GetEncryption();
    }
}