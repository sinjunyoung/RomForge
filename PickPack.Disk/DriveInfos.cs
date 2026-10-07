using PickPack.Disk.ETC;
using System.Management;

namespace PickPack.Disk
{
    public class DriveInfos(string devicePath, string model, long sizeBytes, int diskNumber, string? driveLetter)
    {
        #region Property

        internal static List<DriveInfos> Infos = [];

        public string DevicePath { get; set; } = devicePath;

        public string? DriveLetter { get; private set; } = driveLetter;

        public string Model { get; set; } = model;

        public string DeviceId { get; set; } = devicePath;

        public long SizeBytes { get; set; } = sizeBytes;

        public int DiskNumber { get; set; } = diskNumber;

        public string DisplayName => ToString();

        #endregion

        public override string ToString()
        {
            string letter = string.IsNullOrEmpty(DriveLetter) ? "?" : DriveLetter;

            return $"[{letter}] ({FileSize.FormatSize(SizeBytes)}) {Model}";
        }

        public static List<DriveInfos> GetDriveInfos()
        {
            var infos = new List<DriveInfos>();
            var removableLetters = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Removable || d.DriveType == DriveType.Fixed)
                .Where(d => d.IsReady)
                .Select(d => d.Name[..2])
                .ToList();

            if (removableLetters.Count == 0) 
                return infos;

            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");

            foreach (ManagementObject disk in searcher.Get().Cast<ManagementObject>())
            {
                int diskNumber = Convert.ToInt32(disk["Index"]);
                string deviceId = disk["DeviceID"]?.ToString() ?? string.Empty;
                string model = disk[nameof(Model)]?.ToString() ?? "Unknown";
                long size = Convert.ToInt64(disk["Size"] ?? 0);

                string? matchedLetter = null;
                var partitions = disk.GetRelated("Win32_DiskPartition");

                foreach (ManagementObject partition in partitions.Cast<ManagementObject>())
                {
                    var logicalDisks = partition.GetRelated("Win32_LogicalDisk");

                    foreach (ManagementObject logical in logicalDisks.Cast<ManagementObject>())
                    {
                        string letter = logical["DeviceID"]?.ToString() ?? string.Empty;

                        if (removableLetters.Contains(letter))
                        {
                            matchedLetter = letter;
                            break;
                        }
                    }
                }

                if (matchedLetter != null)
                    infos.Add(new DriveInfos(deviceId, model, size, diskNumber, matchedLetter));
            }

            return infos;
        }
    }
}