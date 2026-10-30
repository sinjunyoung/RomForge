using System.Runtime.InteropServices;

namespace RomForge.Core;

public static class ClipboardHelper
{
    public static bool CopyText(string text)
    {
        if (string.IsNullOrEmpty(text)) 
            return false;

        for (int i = 0; i < 5; i++)
        {
            if (Common.Win32API.OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    Common.Win32API.EmptyClipboard();

                    int bytes = (text.Length + 1) * sizeof(char);
                    IntPtr hGlobal = Common.Win32API.GlobalAlloc(Common.Win32API.GMEM_MOVEABLE, (UIntPtr)bytes);

                    if (hGlobal == IntPtr.Zero)
                        continue;

                    IntPtr target = Common.Win32API.GlobalLock(hGlobal);

                    if (target != IntPtr.Zero)
                    {
                        Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                        Marshal.WriteInt16(target, text.Length * sizeof(char), 0);
                        Common.Win32API.GlobalUnlock(hGlobal);
                    }

                    if (Common.Win32API.SetClipboardData(Common.Win32API.CF_UNICODETEXT, hGlobal) != IntPtr.Zero)
                        return true;
                }
                finally
                {
                    Common.Win32API.CloseClipboard();
                }
            }

            Thread.Sleep(30);
        }

        return false;
    }
}