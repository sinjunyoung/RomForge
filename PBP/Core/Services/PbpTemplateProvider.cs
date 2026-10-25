namespace PBP.Core.Services;

internal static class PbpTemplateProvider
{
    public static byte[] GetBaseHeaderTemplate() => Properties.Resources.DATA1;

    public static byte[] GetBaseFooterTemplate() => Properties.Resources.DATA2;

    public static byte[] GetSystemConfigTemplate() => Properties.Resources.DATA3;
}