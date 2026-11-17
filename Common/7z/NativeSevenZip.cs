using Common.Localization;
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

    [LibraryImport("kernel32.dll", SetLastError = true, EntryPoint = "GetModuleHandleW", StringMarshalling = StringMarshalling.Utf16)]
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
                throw new FileNotFoundException(Loc.S.ErrNative7zLoadFailed, ex);
            }
            catch (EntryPointNotFoundException) { }

            IntPtr handle = GetModuleHandle("7z.dll");

            if (handle == IntPtr.Zero)
                throw new FileNotFoundException(Loc.S.Err7zModuleHandleNotFound);

            var sb = new StringBuilder(1024);
            int len = GetModuleFileName(handle, sb, sb.Capacity);

            if (len == 0)
                throw new FileNotFoundException(Loc.S.Err7zLoadPathUnknown);

            SevenZipExtractor.SetLibraryPath(sb.ToString());

            _initialized = true;
        }
    }
}