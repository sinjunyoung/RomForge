using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text;

namespace PickPack.Disk.Helpers
{
    internal static partial class Win32API
    {
        public const uint IOCTL_DISK_DELETE_DRIVE_LAYOUT = 0x0007C100;
        public const uint IOCTL_DISK_CREATE_DISK = 0x0007C058;
        public const uint IOCTL_DISK_GET_DRIVE_GEOMETRY = 0x00070000;
        public const uint IOCTL_DISK_UPDATE_PROPERTIES = 0x00070140;
        public const uint FILE_VOLUME_IS_SYSTEM = 0x00002000;
        public const uint FILE_SYSTEM_IS_HIDDEN = 0x00010000;
        public const uint ES_CONTINUOUS = 0x80000000;
        public const uint ES_SYSTEM_REQUIRED = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        public struct DISK_GEOMETRY
        {
            public long Cylinders;

            public int MediaType;

            public int TracksPerCylinder;

            public int SectorsPerTrack;

            public uint BytesPerSector;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CREATE_DISK_GPT
        {
            public Guid DiskId;

            public uint MaxPartitionCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CREATE_DISK
        {
            public int PartitionStyle;

            public CREATE_DISK_GPT Gpt;
        }

        [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        public static partial SafeFileHandle CreateFile(string lpFileName, FileAccess dwDesiredAccess, FileShare dwShareMode, IntPtr lpSecurityAttributes, FileMode dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool DeleteVolumeMountPoint(string lpszVolumeMountPoint);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool DeviceIoControl(SafeFileHandle hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize, IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

        [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        public static partial IntPtr FindFirstVolume([Out] char[] lpszVolumeName, uint cchBufferLength);

        [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool FindNextVolume(IntPtr hFindVolume, [Out] char[] lpszVolumeName, uint cchBufferLength);

        [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetVolumePathNamesForVolumeName(string lpszVolumeName, [Out] char[]? lpszVolumePathNames, uint cchBufferLength, out uint lpcchReturnLength);

        [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetVolumeInformation(string lpRootPathName, [Out] char[]? lpVolumeNameBuffer, uint nVolumeNameSize, out uint lpVolumeSerialNumber, out uint lpMaximumComponentLength, out uint lpFileSystemFlags, [Out] char[] lpFileSystemNameBuffer, uint nFileSystemNameSize);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool FindVolumeClose(IntPtr hFindVolume);

        [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        public static partial uint GetDriveType(string lpRootPathName);

        [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetVolumeMountPoint(string lpszVolumeMountPoint, string lpszVolumeName);

        [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GetDiskFreeSpaceEx(string lpDirectoryName, out ulong lpFreeBytesAvailable, out ulong lpTotalNumberOfBytes, out ulong lpTotalNumberOfFreeBytes);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        public static partial uint SetThreadExecutionState(uint esFlags);
    }
}