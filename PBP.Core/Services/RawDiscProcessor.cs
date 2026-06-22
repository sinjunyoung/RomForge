using CHD.Core.Interop;
using CHD.Core.Interop.Enums;
using CHD.Core.Services;

namespace PBP.Core.Services;

public static class RawDiscProcessor
{
    public static ResolvedDisc Resolve(string filePath)
    {
        string ext = Path.GetExtension(filePath).ToLower();

        if (ext == ".cue")
            return CuePreprocessor.Resolve(filePath);
        else if (ext == ".chd")
        {
            var wrapper = new LibChdrWrapper();
            var error = wrapper.Open(filePath);
            if (error != ChdrError.CHDERR_NONE)
                throw new Exception($"CHD 열기 실패: {LibChdrWrapper.GetErrorString(error)}");

            var info = ChdInfoReader.ReadChdInfo(filePath);

            long totalSize = ChdmanService.CalculateOriginalSize(info);
            var stream = new ChdReadStream(wrapper, totalSize, info);
            var cueFile = CueFileReader.BuildCueFromChdInfo(info);
            byte[] tocData = TocBuilder.BuildToc(cueFile, (uint)totalSize);

            return ResolvedDisc.Create(stream, stream.Length, tocData);
        }

        throw new NotSupportedException($"지원하지 않는 확장자: {ext}");
    }
}