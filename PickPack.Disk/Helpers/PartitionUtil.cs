using System.ComponentModel;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

namespace PickPack.Disk.Helpers
{
    public static class PartitionUtil
    {
        static readonly string[] SafeFileSystems = ["FAT32", "NTFS", "exFAT"];

        private static int GetPartitionCountByDiskNumber(int diskNumber)
        {
            using var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_DiskPartition WHERE DiskIndex = {diskNumber}");
            return searcher.Get().Count;
        }

        private static List<string> GetDriveLettersByDiskNumber(int diskNumber)
        {
            var driveLetters = new List<string>();
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");

            foreach (ManagementObject disk in searcher.Get().Cast<ManagementObject>())
            {
                if (Convert.ToInt32(disk["Index"]) == diskNumber)
                {
                    foreach (ManagementObject partition in disk.GetRelated("Win32_DiskPartition").Cast<ManagementObject>())
                    {
                        foreach (ManagementObject logical in partition.GetRelated("Win32_LogicalDisk").Cast<ManagementObject>())
                        {
                            string? deviceId = logical["DeviceID"]?.ToString();

                            if (!string.IsNullOrEmpty(deviceId))
                                driveLetters.Add(deviceId);
                        }
                    }

                    break;
                }
            }

            return driveLetters;
        }

        private static List<string> FindUnassignedVolumeGuids()
        {
            var result = new List<string>();
            var volumeNameChars = new char[260];
            IntPtr findHandle = Win32API.FindFirstVolume(volumeNameChars, (uint)volumeNameChars.Length);

            if (findHandle == IntPtr.Zero)
                return result;

            try
            {
                do
                {
                    string currentVolumeGuid = new string(volumeNameChars).TrimEnd('\0');

                    Win32API.GetVolumePathNamesForVolumeName(currentVolumeGuid, null, 0, out uint pathLength);

                    if (pathLength != 2)
                        continue;

                    uint driveType = Win32API.GetDriveType(currentVolumeGuid);

                    if (driveType != 2)
                        continue;

                    var fsNameChars = new char[260];

                    if (!Win32API.GetVolumeInformation(currentVolumeGuid, null, 0, out _, out _, out uint fileSystemFlags, fsNameChars, (uint)fsNameChars.Length))
                        continue;

                    string fileSystem = new string(fsNameChars).TrimEnd('\0');

                    if (SafeFileSystems.Contains(fileSystem, StringComparer.OrdinalIgnoreCase) && !string.IsNullOrEmpty(fileSystem) && (fileSystemFlags & Win32API.FILE_SYSTEM_IS_HIDDEN) == 0 && (fileSystemFlags & Win32API.FILE_VOLUME_IS_SYSTEM) == 0)
                        result.Add(currentVolumeGuid);

                } while (Win32API.FindNextVolume(findHandle, volumeNameChars, (uint)volumeNameChars.Length));
            }
            finally
            {
                Win32API.FindVolumeClose(findHandle);
            }

            return result;
        }

        private static bool AssignDriveLetter(string volumeGuid, char? driveLetter)
        {
            if (driveLetter == null)
                return false;

            string mountPoint = $"{driveLetter}:\\";

            return Win32API.SetVolumeMountPoint(mountPoint, volumeGuid);
        }

        public static char? FindFirstAvailableDriveLetter()
        {
            var usedLetters = new HashSet<char>(DriveInfo.GetDrives()
                .Where(d => d.Name.Length >= 2 && char.IsLetter(d.Name[0]))
                .Select(d => d.Name[0])
                .Select(char.ToUpper)
            );

            for (char letter = 'C'; letter <= 'Z'; letter++)
            {
                if (!usedLetters.Contains(letter))
                    return letter;
            }

            return null;
        }

        private static void DeleteAndInitPartition(int diskNumber)
        {
            string diskPath = $@"\\.\PhysicalDrive{diskNumber}";

            using var handle = Win32API.CreateFile(diskPath, FileAccess.ReadWrite, FileShare.None, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);

            if (handle.IsInvalid)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "디스크 핸들 열기 실패.");

            bool result = Win32API.DeviceIoControl(handle, Win32API.IOCTL_DISK_DELETE_DRIVE_LAYOUT, IntPtr.Zero, 0, IntPtr.Zero, 0, out uint bytesReturned, IntPtr.Zero);

            if (!result)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "파티션 삭제 실패.");

            Win32API.CREATE_DISK createDisk = new()
            {
                PartitionStyle = 1,
                Gpt = new Win32API.CREATE_DISK_GPT
                {
                    DiskId = Guid.NewGuid(),
                    MaxPartitionCount = 128
                }
            };

            int size = Marshal.SizeOf(createDisk);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(createDisk, ptr, false);

            result = Win32API.DeviceIoControl(handle, Win32API.IOCTL_DISK_CREATE_DISK, ptr, (uint)size, IntPtr.Zero, 0, out bytesReturned, IntPtr.Zero);

            if (!result)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "GPT 초기화 실패.");

            Marshal.FreeHGlobal(ptr);
        }

        public static async Task DeleteAllPartitionsAsync(int diskNumber)
        {
            int count = GetPartitionCountByDiskNumber(diskNumber);

            if (count > 0)
            {
                var letters = GetDriveLettersByDiskNumber(diskNumber);

                foreach (var letter in letters)
                    Win32API.DeleteVolumeMountPoint($"{letter}\\");

                await Task.Run(() => DeleteAndInitPartition(diskNumber));
            }
        }

        public static void RescanDisk(int diskNumber)
        {
            string diskPath = $@"\\.\PhysicalDrive{diskNumber}";
            using var handle = Win32API.CreateFile(diskPath, FileAccess.ReadWrite, FileShare.None, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero);

            Win32API.DeviceIoControl(handle, Win32API.IOCTL_DISK_UPDATE_PROPERTIES, IntPtr.Zero, 0, IntPtr.Zero, 0, out uint bytesReturned2, IntPtr.Zero);
        }

        public static void AssignNextAvailableDriveLetter()
        {
            var guids = FindUnassignedVolumeGuids();

            for (int i = 0; i < guids.Count; i++)
                AssignDriveLetter(guids[i], FindFirstAvailableDriveLetter());
        }
    }
}