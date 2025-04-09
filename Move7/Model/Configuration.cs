namespace Move7.Model
{
    public static class Configuration
    {
        public static int Duration { get; set; }
        public static string BackupPath { get; set; }
        public static long MaxFileSize { get; set; }
        public static string[] Admins { get; set; }
        public static string[] Developers { get; set; }
    }
}
