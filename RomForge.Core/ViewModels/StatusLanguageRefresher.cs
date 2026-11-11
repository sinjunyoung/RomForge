using Common.Localization;

namespace RomForge.Core.ViewModels;

internal static class StatusLanguageRefresher
{
    private const int PruneInterval = 512;

    private static readonly List<WeakReference<ProcessableItemBase>> Items = [];
    private static readonly object Gate = new();

    static StatusLanguageRefresher() => Loc.Instance.PropertyChanged += (_, _) => RefreshAll();

    public static void Register(ProcessableItemBase item)
    {
        lock (Gate)
        {
            Items.Add(new WeakReference<ProcessableItemBase>(item));

            if (Items.Count % PruneInterval == 0)
                Items.RemoveAll(x => !x.TryGetTarget(out _));
        }
    }

    private static void RefreshAll()
    {
        List<ProcessableItemBase> alive = [];

        lock (Gate)
        {
            Items.RemoveAll(x => !x.TryGetTarget(out _));

            foreach (var reference in Items)
            {
                if (reference.TryGetTarget(out var item))
                    alive.Add(item);
            }
        }

        foreach (var item in alive)
            item.RefreshStatus();
    }
}