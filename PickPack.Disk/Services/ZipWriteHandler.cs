using PickPack.Disk.Interfaces;
using PickPack.Disk.IO;

namespace PickPack.Disk.Services
{
    public class ZipWriteHandler : IImageWriteHandler
    {
        public async Task<(Stream stream, long length)> OpenStreamAsync(string imagePath, CancellationToken ct)
        {
            await Task.Yield();

            var zipFile = Ionic.Zip.ZipFile.Read(imagePath);

            try
            {
                var entry = zipFile.Entries.FirstOrDefault(e => e.FileName.EndsWith(".img", StringComparison.OrdinalIgnoreCase)) ?? throw new InvalidOperationException("ZIP 파일 안에 IMG 파일이 없습니다.");
                var entryStream = entry.OpenReader();
                var wrapperStream = new CompositeDisposableStream(entryStream, zipFile);
                return (wrapperStream, entry.UncompressedSize);
            }
            catch
            {
                zipFile.Dispose();
                throw;
            }
        }
    }
}