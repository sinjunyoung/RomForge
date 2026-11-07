namespace Common.Services
{
    public static class FileSize
    {
        public const long _1GB = 1073741824;

        public static string FormatSize(long bytes)
        {
            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;
            const long TB = GB * 1024;

            if (bytes >= TB)
                return $"{(bytes / (double)TB):N2} TB";

            if (bytes >= GB)
                return $"{(bytes / (double)GB):N2} GB";

            if (bytes >= MB)
                return $"{(bytes / (double)MB):N2} MB";

            if (bytes >= KB)
                return $"{(bytes / (double)KB):N2} KB";

            return $"{bytes} Bytes";
        }
    }
}