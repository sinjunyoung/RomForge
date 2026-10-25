using N3DS.Save.Models;

namespace N3DS.Save;

public class FsMeta(MetaTable dirs, MetaTable files)
{
    public readonly MetaTable Dirs = dirs;
    public readonly MetaTable Files = files;

    public MetaStat Stat() => new()
    {
        Dirs = Dirs.Stat(),
        Files = Files.Stat(),
    };
}