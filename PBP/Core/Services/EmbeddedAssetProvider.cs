namespace PBP.Core.Services;

public static class EmbeddedAssetProvider
{
    public static byte[] GetDefaultIcon0() => Properties.Resources.ICON0;
    
    public static byte[] GetDefaultPic0() => Properties.Resources.PIC0;
    
    public static byte[] GetDefaultPic1() => Properties.Resources.PIC1;
    
    public static byte[] GetGamesDatabase() => Properties.Resources.GamesDB;

    public static byte[] GetDefaultData() => Properties.Resources.DATA;

    public static byte[] GetBlankImage() => Properties.Resources.BLANK;
}