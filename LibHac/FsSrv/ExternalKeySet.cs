using System;
using System.Collections.Generic;
using System.Threading;
using LibHac.Fs;
using LibHac.Spl;

namespace LibHac.FsSrv;

public class ExternalKeySet
{
    private readonly Lock _locker = new();

    private Dictionary<RightsId, AccessKey> ExternalKeys { get; set; } = new();

    public Result Add(RightsId rightsId, AccessKey key)
    {
        lock (_locker)
        {
            if (ExternalKeys.TryGetValue(rightsId, out AccessKey existingKey))
            {
                if (key == existingKey)
                {
                    return Result.Success;
                }

                return ResultFs.NcaExternalKeyInconsistent.Log();
            }

            ExternalKeys.Add(rightsId, key);
        }

        return Result.Success;
    }

    public Result Get(RightsId rightsId, out AccessKey key)
    {
        lock (_locker)
        {
            if (ExternalKeys.TryGetValue(rightsId, out key))
            {
                return Result.Success;
            }

            return ResultFs.NcaExternalKeyUnregistered.Log();
        }
    }

    public bool Contains(RightsId rightsId)
    {
        lock (_locker)
        {
            return ExternalKeys.ContainsKey(rightsId);
        }
    }

    public bool Remove(RightsId rightsId)
    {
        lock (_locker)
        {
            return ExternalKeys.Remove(rightsId);
        }
    }

    public void Clear()
    {
        lock (_locker)
        {
            ExternalKeys.Clear();
        }
    }

    public List<(RightsId rightsId, AccessKey key)> ToList()
    {
        lock (_locker)
        {
            List<(RightsId rightsId, AccessKey key)> list = new(ExternalKeys.Count);

            foreach (KeyValuePair<RightsId, AccessKey> kvp in ExternalKeys)
            {
                list.Add((kvp.Key, kvp.Value));
            }

            return list;
        }
    }

    public void TrimExcess() => TrimExcess(0);

    public void TrimExcess(int capacity)
    {
        lock (_locker)
        {
            int newCapacity = Math.Max(capacity, ExternalKeys.Count);
            ExternalKeys.TrimExcess(newCapacity);
        }
    }

    public void EnsureCapacity(int capacity)
    {
        ExternalKeys.EnsureCapacity(capacity);
    }
}