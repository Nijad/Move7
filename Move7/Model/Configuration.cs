namespace Move7.Model
{
    internal class Configuration
    {
        public static int Duration { get; set; }
        public static string BackupPath { get; set; }
        public static long MaxFileSize { get; set; }
        public static List<string> Extensions { get; set; }
        public static List<string> Admins { get; set; }
        public static List<string> Developers { get; set; }
    }
}
