using System.Text;
using Common;

namespace Patch.Core.Formats;

public static class Ppf
{
    private static readonly byte[] HeaderMagic30 = [(byte)'P', (byte)'P', (byte)'F', (byte)'3', (byte)'0'];
    private const int DescriptionOffset = 6;
    private const int DescriptionLength = 50;

    public static async Task ApplyPatchAsync(string sourcePath, string patchPath, string outputPath, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        ValidateInputFiles(sourcePath, patchPath);

        await Task.Run(() =>
        {
            using var srcStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var patStream = new FileStream(patchPath, FileMode.Open, FileAccess.Read);
            using var outStream = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite);

            DecodeStreamInternal(srcStream, patStream, outStream, progress, ct);
        }, ct).ConfigureAwait(false);
    }

    public static async Task<byte[]> ApplyPatchAsync(byte[] sourceData, byte[] patchData, IProgress<ProgressInfo>? progress = null, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            using var srcStream = new MemoryStream(sourceData);
            using var patStream = new MemoryStream(patchData);
            using var outStream = new MemoryStream();

            DecodeStreamInternal(srcStream, patStream, outStream, progress, ct);

            return outStream.ToArray();
        }, ct).ConfigureAwait(false);
    }

    private static void DecodeStreamInternal(Stream src, Stream pat, Stream outStream, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        if (pat.Length < 56)
            throw new InvalidDataException("PPF 파일이 너무 짧습니다.");

        byte[] header = new byte[60];

        pat.ReadExactly(header, 0, 60);

        if (header[0] != 'P' || header[1] != 'P' || header[2] != 'F')
            throw new InvalidDataException("유효하지 않은 PPF 헤더입니다.");

        byte version = (byte)(header[3] - '0');

        if (version < 1 || version > 3)
            throw new InvalidDataException($"지원하지 않는 PPF 버전입니다: PPF {version}.0");

        long dataEnd = pat.Length;
        bool isUndoAvailable = false;

        if (version >= 2 && pat.Length > 10)
        {
            int lenIdx = version == 2 ? 4 : 2;

            if (pat.Length > lenIdx + 4)
            {
                pat.Position = pat.Length - lenIdx - 4;

                byte[] tail = new byte[4];

                pat.ReadExactly(tail, 0, 4);

                if (tail[0] == '.' && tail[1] == 'D' && tail[2] == 'I' && tail[3] == 'Z')
                {
                    pat.Position = pat.Length - lenIdx;

                    byte[] lenBuf = new byte[lenIdx];

                    pat.ReadExactly(lenBuf, 0, lenIdx);

                    int idLen = version == 2 ? BitConverter.ToInt32(lenBuf, 0) : BitConverter.ToUInt16(lenBuf, 0);

                    dataEnd -= version == 2 ? idLen + 38 : idLen + 36;
                }
            }
        }

        long pos = 60;

        if (version == 1)
            pos = 56;
        else if (version == 2)
        {
            ValidateBlockcheckInternal(pat, src, 0, 2);
            pos = 1084;
        }
        else
        {
            byte blockcheck = header[57];

            isUndoAvailable = header[58] == 1;

            byte imagetype = header[56];

            if (blockcheck != 0)
            {
                ValidateBlockcheckInternal(pat, src, imagetype, 3);

                pos = 1084;
            }
        }

        pat.Position = pos;

        long srcSize = src.Length;
        long patchDataStartPos = pos;
        long patchDataSize = dataEnd - patchDataStartPos;
        long totalWork = srcSize;
        var reporter = new ProgressReporter("패치중...", string.Empty, totalWork, progress);
        Action<long, long>? report = reporter.CreateAction();
        long nextReport = Environment.TickCount64 + 100;
        const double copyWeight = 0.60;
        const double patchWeight = 0.40;

        src.Position = 0;
        outStream.Position = 0;

        byte[] ioBuffer = new byte[64 * 1024];
        long totalCopied = 0;

        while (totalCopied < srcSize)
        {
            ct.ThrowIfCancellationRequested();

            int read = src.Read(ioBuffer, 0, (int)Math.Min(ioBuffer.Length, srcSize - totalCopied));

            if (read == 0)
                break;

            outStream.Write(ioBuffer, 0, read);

            totalCopied += read;

            long now = Environment.TickCount64;

            if (now >= nextReport)
            {
                nextReport = now + 100;

                double copyProgressPct = (double)totalCopied / srcSize * copyWeight;
                long virtualReportedBytes = (long)(totalWork * copyProgressPct);

                report?.Invoke(virtualReportedBytes, totalWork);
            }
        }

        byte[] buffer = new byte[256];

        while (pat.Position < dataEnd)
        {
            ct.ThrowIfCancellationRequested();

            long offset = version == 3 ? BitConverter.ToInt64(ReadBytes(pat, 8), 0) : BitConverter.ToUInt32(ReadBytes(pat, 4), 0);
            int length = pat.ReadByte();

            if (length <= 0 || pat.Position + length > dataEnd)
                break;

            pat.ReadExactly(buffer, 0, length);

            if (offset >= 0)
            {
                outStream.Position = offset;

                outStream.Write(buffer, 0, length);
            }

            if (version == 3 && isUndoAvailable)
                pat.Position += length;

            long now = Environment.TickCount64;

            if (now >= nextReport)
            {
                nextReport = now + 100;

                long currentPatRead = pat.Position - patchDataStartPos;
                double patchProgressPct = (double)currentPatRead / patchDataSize;
                double totalProgressPct = copyWeight + (patchProgressPct * patchWeight);
                long virtualReportedBytes = (long)(totalWork * Math.Min(totalProgressPct, 0.995));

                report?.Invoke(virtualReportedBytes, totalWork);
            }
        }

        report?.Invoke(totalWork, totalWork);
    }

    private static byte[] ReadBytes(Stream stream, int count)
    {
        byte[] buffer = new byte[count];
        stream.ReadExactly(buffer, 0, count);

        return buffer;
    }

    private static void ValidateBlockcheckInternal(Stream pat, Stream src, byte imagetype, int version)
    {
        long sourceBlockStart = imagetype != 0 ? 0x80A0L : 0x9320L;

        if (src.Length < sourceBlockStart + 1024)
            throw new InvalidDataException("소스 파일이 너무 짧아 blockcheck를 수행할 수 없습니다.");

        byte[] patchBlock = new byte[1024];

        pat.Position = 60;

        pat.ReadExactly(patchBlock, 0, 1024);

        byte[] sourceBlock = new byte[1024];

        src.Position = sourceBlockStart;

        src.ReadExactly(sourceBlock, 0, 1024);

        for (int i = 0; i < 1024; i++)
            if (patchBlock[i] != sourceBlock[i])
                throw new InvalidDataException($"Blockcheck 실패 (PPF {version}.0)");
    }

    private static void ValidateInputFiles(params string[] paths)
    {
        foreach (var path in paths)
            if (!File.Exists(path))
                throw new FileNotFoundException($"파일을 찾을 수 없습니다: {path}");
    }
}