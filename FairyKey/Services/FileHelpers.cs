using System.IO;

namespace FairyKey
{
    public static class FileHelpers
    {
        public static string[] SafeReadAllLines(string path, int retries = 3, int delayMs = 50)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    return File.ReadAllLines(path);
                }
                catch (IOException) { Thread.Sleep(delayMs); }
                catch (UnauthorizedAccessException) { Thread.Sleep(delayMs); }
            }
            return Array.Empty<string>();
        }
    }
}