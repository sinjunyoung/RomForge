using System.Diagnostics;
using WiiU.WUP.Crypto;

namespace WiiU.WUP.Services
{
    public class NUSPackage
    {
        public Ticket Ticket { get; set; } = null!;

        public TMD TMD { get; set; } = null!;

        public FST FST { get; set; } = null!;

        public string Outputdir { get; set; } = "output";

        public Contents Contents => FST.Contents;

        public ContentInfos ContentInfos => TMD.ContentInfos;

        public Encryption Encryption => TMD.Encryption;

        public void PackContents(string outputDir, Action<Content>? onContentPacked, Action<Content, string, long, long>? onContentBytesProcessed)
        {
            if (!string.IsNullOrEmpty(outputDir))
                Outputdir = outputDir;

            try
            {
                FST.Contents.PackContents(Outputdir, onContentPacked, onContentBytesProcessed);
            }
            catch (IOException e1)
            {
                Debug.WriteLine(e1);
            }

            Content fstContent = Contents.FSTContent;

            fstContent.Hash = HashUtil.HashSHA1(FST.GetAsData());
            fstContent.EncryptedFileSize = FST.GetAsData().Length;

            ContentInfo contentInfo = ContentInfos[0];

            contentInfo.SHA2Hash = HashUtil.HashSHA2(Contents.GetAsData());

            TMD.UpdateContentInfoHash();

            try
            {
                using (FileStream fos = new(Path.Combine(Outputdir, "title.tmd"), FileMode.Create, FileAccess.Write))
                {
                    byte[] data = TMD.GetAsData();

                    fos.Write(data, 0, data.Length);
                }

                using (FileStream fos = new(Path.Combine(Outputdir, "title.cert"), FileMode.Create, FileAccess.Write))
                {
                    byte[] data = Cert.GetCertAsData();

                    fos.Write(data, 0, data.Length);
                }

                using (FileStream fos = new(Path.Combine(Outputdir, "title.tik"), FileMode.Create, FileAccess.Write))
                {
                    byte[] data = Ticket.GetAsData();

                    fos.Write(data, 0, data.Length);
                }
            }
            catch (IOException e)
            {
                Debug.WriteLine(e);
            }
        }
    }
}