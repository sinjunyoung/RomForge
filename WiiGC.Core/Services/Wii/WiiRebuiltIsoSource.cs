using WiiGC.Core.Models;

namespace WiiGC.Core.Services.Wii;

internal sealed class WiiRebuiltIsoSource : IRvzInputSource
{
    private const int MaxCachedGroups = 32;

    private sealed record Target(int Index, WiiPartitionSpec Spec, WiiRepackPlan Plan);

    private readonly IRvzInputSource _input;
    private readonly List<WiiPartitionReader> _readers = [];
    private readonly List<Target> _targets = [];
    private readonly List<(long Offset, byte[] Data)> _patches = [];
    private readonly Dictionary<(int Partition, long Group), byte[]> _cache = [];
    private readonly Queue<(int Partition, long Group)> _order = new();
    private readonly WiiPartitionEncoder _encoder = new();
    private readonly byte[] _scratch = new byte[WiiLayout.HashSize];
    private readonly object _gate = new();
    private readonly bool _ownsInput;

    private WiiRebuiltIsoSource(IRvzInputSource input, bool ownsInput = false)
    {
        _input = input;
        _ownsInput = ownsInput;
    }

    public long Length => _input.Length;

    public WiiPatchResult Result { get; private set; } = WiiPatchResult.Empty;

    public static WiiRebuiltIsoSource Create(IRvzInputSource input, IReadOnlyDictionary<string, string> replacements, Action<double>? progress, Action<WiiPatchEntry>? entryLog, CancellationToken ct)
    {
        if (replacements.Count == 0)
            throw new InvalidDataException("교체할 파일이 없습니다.");

        var source = new WiiRebuiltIsoSource(input);

        try
        {
            source.Plan(replacements);

            source.Result = new WiiPatchResult(source._targets[0].Plan.Entries);

            source.Prepare(progress, entryLog, ct);

            return source;
        }
        catch
        {
            source.Dispose();

            throw;
        }
    }

    public static WiiRebuiltIsoSource CreateFromFolder(string folder, IReadOnlyDictionary<string, string>? overlay, Action<double>? progress, Action<WiiPatchEntry>? entryLog, CancellationToken ct)
    {
        var info = WiiFolderInfo.Read(folder);
        var plan = WiiFolderPlanner.Plan(folder, overlay);
        WiiFolderBaseSource? baseSource = null;
        WiiRebuiltIsoSource? source = null;

        try
        {
            baseSource = WiiFolderBaseSource.Create(folder, info, plan.DataSize);
            source = new WiiRebuiltIsoSource(baseSource, true);

            source._targets.Add(new Target(0, baseSource.CreateSpec(plan.DataSize), plan));

            source.Result = new WiiPatchResult(plan.Entries);

            source.Prepare(progress, entryLog, ct);

            return source;
        }
        catch
        {
            if (source != null)
                source.Dispose();
            else
            {
                plan.Data.Dispose();
                baseSource?.Dispose();
            }

            throw;
        }
    }

    public void Read(long offset, Span<byte> destination)
    {
        long end = offset + destination.Length;
        long position = offset;

        while (position < end)
        {
            var target = FindTarget(position, out long nextStart);
            int done = (int)(position - offset);

            if (target != null)
            {
                long stop = Math.Min(end, target.Spec.DataStart + target.Spec.DataSize);

                ReadPartition(target, position, destination.Slice(done, (int)(stop - position)));

                position = stop;
            }
            else
            {
                long stop = Math.Min(end, nextStart);

                _input.Read(position, destination.Slice(done, (int)(stop - position)));

                position = stop;
            }
        }

        ApplyPatches(offset, destination);
    }

    public void Dispose()
    {
        _encoder.Dispose();

        foreach (var target in _targets)
            target.Plan.Data.Dispose();

        foreach (var reader in _readers)
            reader.Dispose();

        if (_ownsInput)
            _input.Dispose();
    }

    private void Plan(IReadOnlyDictionary<string, string> replacements)
    {
        var specs = WiiIsoRebuilder.ReadSpecs(_input);
        long cursor = 0;

        foreach (var spec in WiiIsoRebuilder.FindTargets(_input, specs).OrderBy(s => s.DataStart))
        {
            if (spec.DataStart < cursor)
                throw new InvalidDataException("Wii 파티션 데이터 영역이 겹칩니다.");

            var reader = new WiiPartitionReader(_input, spec);

            _readers.Add(reader);

            var plan = WiiPartitionPlanner.Plan(reader, replacements);

            if (plan.DataSize > spec.DataSize)
            {
                plan.Data.Dispose();

                throw new InvalidDataException("교체 후 파티션 크기가 원본 파티션 영역을 초과합니다.");
            }

            _targets.Add(new Target(_targets.Count, spec, plan));

            cursor = spec.DataStart + spec.DataSize;
        }

        if (_targets.Count == 0)
            throw new InvalidDataException("게임 파티션을 찾을 수 없습니다.");
    }

    private void Prepare(Action<double>? progress, Action<WiiPatchEntry>? entryLog, CancellationToken ct)
    {
        long total = _targets.Sum(t => WiiPartitionEncoder.GetGroupCount(t.Plan.Data));
        long done = 0;
        int lastPermille = -1;

        foreach (var target in _targets)
        {
            var data = target.Plan.Data;
            long groups = WiiPartitionEncoder.GetGroupCount(data);

            if (groups * WiiLayout.HashSize > WiiPartitionHeader.H3TableSize)
                throw new InvalidDataException("파티션이 H3 테이블 용량을 초과합니다.");

            byte[] h3 = new byte[WiiPartitionHeader.H3TableSize];
            var logger = target.Index == 0 ? new WiiPatchLogger(target.Plan.Entries, entryLog) : null;

            for (long group = 0; group < groups; group++)
            {
                ct.ThrowIfCancellationRequested();
                logger?.Advance((group + 1) * WiiLayout.GroupDataSize);
                _encoder.EncodeGroup(target.Spec.Key, data, group, h3.AsSpan((int)(group * WiiLayout.HashSize), WiiLayout.HashSize));

                done++;

                int permille = (int)(done * 1000 / total);

                if (permille != lastPermille)
                {
                    lastPermille = permille;

                    progress?.Invoke(permille / 1000.0);
                }
            }

            logger?.Complete();
            _patches.AddRange(WiiIsoRebuilder.BuildHeaderPatches(_input, target.Spec, h3, target.Plan.DataSize));
        }

        progress?.Invoke(1);
    }

    private Target? FindTarget(long position, out long nextStart)
    {
        nextStart = long.MaxValue;

        foreach (var target in _targets)
        {
            if (position < target.Spec.DataStart)
            {
                nextStart = target.Spec.DataStart;

                return null;
            }

            if (position < target.Spec.DataStart + target.Spec.DataSize)
                return target;
        }

        return null;
    }

    private void ReadPartition(Target target, long position, Span<byte> destination)
    {
        long relative = position - target.Spec.DataStart;
        long encodedSize = target.Plan.DataSize;
        int written = 0;

        while (written < destination.Length)
        {
            long current = relative + written;
            int remaining = destination.Length - written;

            if (current >= encodedSize)
            {
                destination[written..].Clear();

                return;
            }

            long group = current / WiiLayout.GroupTotalSize;
            int within = (int)(current - group * WiiLayout.GroupTotalSize);
            int take = (int)Math.Min(remaining, Math.Min(WiiLayout.GroupTotalSize - within, encodedSize - current));
            byte[] data = GetGroup(target, group);
            int copy = Math.Clamp(data.Length - within, 0, take);

            if (copy > 0)
                data.AsSpan(within, copy).CopyTo(destination[written..]);

            if (copy < take)
                destination.Slice(written + copy, take - copy).Clear();

            written += take;
        }
    }

    private byte[] GetGroup(Target target, long group)
    {
        var key = (target.Index, group);

        lock (_gate)
        {
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            int length = _encoder.EncodeGroup(target.Spec.Key, target.Plan.Data, group, _scratch);
            byte[] encoded = _encoder.Encrypted[..length].ToArray();

            _cache[key] = encoded;
            _order.Enqueue(key);

            while (_order.Count > MaxCachedGroups)
                _cache.Remove(_order.Dequeue());

            return encoded;
        }
    }

    private void ApplyPatches(long offset, Span<byte> destination)
    {
        long end = offset + destination.Length;

        foreach (var (patchOffset, data) in _patches)
        {
            long from = Math.Max(offset, patchOffset);
            long to = Math.Min(end, patchOffset + data.Length);

            if (to > from)
                data.AsSpan((int)(from - patchOffset), (int)(to - from)).CopyTo(destination[(int)(from - offset)..]);
        }
    }
}