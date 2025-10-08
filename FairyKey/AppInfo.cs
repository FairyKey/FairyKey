namespace FairyKey
{
    public static class AppInfo
    {
        public const string AppName = "Fairy Key";
        public const string Author = "@ikin-dev";
        public const string Website = "https://fairykey.app";
        public const string GitHub = "https://github.com/FairyKey/FairyKey";
        public static string Version
        {
            get
            {
                var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                if (ver == null) return "Unknown Version";
                return ver.Revision == 0
                    ? $"{ver.Major}.{ver.Minor}.{ver.Build}"
                    : ver.ToString();
            }
        }
    }
}