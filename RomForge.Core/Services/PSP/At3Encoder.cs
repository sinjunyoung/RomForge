using PSP.PBP.AT3.Models;
using PSP.PBP.AT3.Services;

namespace RomForge.Core.Services.PSP
{
    public static class At3Encoder
    {
        public static byte[] Convert(byte[] inputBytes, bool isWav, ConverterOptions? options = null) => ConverterService.Convert(inputBytes, isWav, options);
    }
}