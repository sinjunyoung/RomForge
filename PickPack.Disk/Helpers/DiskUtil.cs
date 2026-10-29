using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace PickPack.Disk.Helpers
{
    public static class DiskUtil
    {    
        public static int GetSectorSize(SafeFileHandle diskHandle)
        {
            int size = Marshal.SizeOf<Win32API.DISK_GEOMETRY>();
            IntPtr buffer = Marshal.AllocHGlobal(size);

            try
            {
                if (Win32API.DeviceIoControl(diskHandle, Win32API.IOCTL_DISK_GET_DRIVE_GEOMETRY, IntPtr.Zero, 0, buffer, (uint)size, out _, IntPtr.Zero))
                {
                    Win32API.DISK_GEOMETRY geometry = Marshal.PtrToStructure<Win32API.DISK_GEOMETRY>(buffer);

                    return (int)geometry.BytesPerSector;
                }
                else
                    return 512;
            }
            finally
            {   
                Marshal.FreeHGlobal(buffer);
            }
        }

        public static long GetDiskLength(int physicalDriveNumber) => GetDiskLength($@"\\.\PhysicalDrive{physicalDriveNumber}");

        public static long GetDiskLength(string physicalDrivePath)
        {
            using var stream = new FileStream(physicalDrivePath, FileMode.Open, FileAccess.ReadWrite);
            return stream.Length;
        }

        public static ulong GetAvailableFreeSpace(string? path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            bool success = Win32API.GetDiskFreeSpaceEx(path, out ulong freeBytesAvailable, out _, out _);

            if (!success)
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "디스크 용량 확인에 실패했습니다.");

            return freeBytesAvailable;
        }

        public static void PreventSleep() => _ = Win32API.SetThreadExecutionState(Win32API.ES_CONTINUOUS | Win32API.ES_SYSTEM_REQUIRED);
    }
}