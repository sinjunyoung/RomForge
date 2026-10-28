using PickPack.Disk.Helpers;
using System.Diagnostics;
using System.Management;

namespace PickPack.Disk.Services;

public class WmiWatchers
{
    readonly ManagementEventWatcher _arrivalWatcher;
    readonly ManagementEventWatcher _removalWatcher;
    readonly Lock _lockObject = new();
    DateTime _lastDriveCheckTime = DateTime.MinValue;

    public event EventHandler? USBArrival;
    public event EventHandler? USBRemoval;

    protected virtual void OnUSBArrival() => USBArrival?.Invoke(this, new EventArgs());

    protected virtual void OnUSBRemoval() => USBRemoval?.Invoke(this, new EventArgs());

    public WmiWatchers()
    {
        WqlEventQuery arrivalQuery = new("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_DiskDrive'");

        _arrivalWatcher = new ManagementEventWatcher(arrivalQuery);
        _arrivalWatcher.EventArrived += (sender, e) =>
        {
            try
            {
                if (HasRemovableDrivesChanged())
                    OnUSBArrival();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WMI Arrival Watcher Error: {ex.Message}");
            }
        };

        _arrivalWatcher.Start();

        WqlEventQuery removalQuery = new("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_DiskDrive'");

        _removalWatcher = new ManagementEventWatcher(removalQuery);

        _removalWatcher.EventArrived += (sender, e) =>
        {
            try
            {
                if (HasRemovableDrivesChanged())
                    OnUSBRemoval();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"WMI Removal Watcher Error: {ex.Message}");
            }
        };

        _removalWatcher.Start();
    }

    private bool HasRemovableDrivesChanged()
    {
        lock (_lockObject)
        {
            try
            {
                if ((DateTime.Now - _lastDriveCheckTime).TotalSeconds < 1)
                    return false;

                var existingItems = DriveInfos.Infos.ToArray();
                var newItems = DriveInfos.GetDriveInfos();

                _lastDriveCheckTime = DateTime.Now;

                if (existingItems.Length != newItems.Count)
                {
                    DriveInfos.Infos.Clear();
                    DriveInfos.Infos.AddRange(newItems);
                    return true;
                }

                var existingDevices = existingItems.Select(x => new { x.DeviceId, x.Model }).OrderBy(x => x.DeviceId).ToList();
                var newDevices = newItems.Select(x => new { x.DeviceId, x.Model }).OrderBy(x => x.DeviceId).ToList();

                for (int i = 0; i < existingDevices.Count; i++)
                {
                    if (existingDevices[i].DeviceId != newDevices[i].DeviceId || existingDevices[i].Model != newDevices[i].Model)
                    {
                        DriveInfos.Infos.Clear();
                        DriveInfos.Infos.AddRange(newItems);
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"HasRemovableDrivesChanged Error: {ex.Message}");
                return false;
            }
        }
    }
}