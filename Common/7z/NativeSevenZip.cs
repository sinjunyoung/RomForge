using System.Runtime.InteropServices;
using System.Text;
using SevenZip;

namespace Patch.Core.Services;

public static partial class NativeSevenZip
{
    private static readonly Lock InitLock = new();
    private static bool _initialized;

    [LibraryImport("7z.dll")]
    private static partial int GetNumberOfFormats(out uint numFormats);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetModuleFileName(IntPtr hModule, StringBuilder lpFilename, int nSize);

    public static void EnsureInitialized()
    {
        if (_initialized)
            return;

        lock (InitLock)
        {
            if (_initialized)
                return;

            try
            {
                int result = GetNumberOfFormats(out _);
            }
            catch (DllNotFoundException ex)
            {
                throw new FileNotFoundException("네이티브 7z.dll을 로드할 수 없습니다. (표준 DllImport 검색으로도 찾지 못함)", ex);
            }
            catch (EntryPointNotFoundException) { }

            IntPtr handle = GetModuleHandle("7z.dll");

            if (handle == IntPtr.Zero)
                throw new FileNotFoundException("7z.dll이 로드된 것 같지만 모듈 핸들을 찾을 수 없습니다.");

            var sb = new StringBuilder(1024);
            int len = GetModuleFileName(handle, sb, sb.Capacity);

            if (len == 0)
                throw new FileNotFoundException("7z.dll의 실제 로드 경로를 확인할 수 없습니다 (GetModuleFileName 실패).");

            SevenZipExtractor.SetLibraryPath(sb.ToString());

            _initialized = true;
        }
    }
}