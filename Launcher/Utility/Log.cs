using System.IO;
using System.Text;

namespace Launcher.Utility
{
    public static class Log
    {
        public static void Error(string message)
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                File.WriteAllText(
                    Path.Combine(baseDir, "launcher_last.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message,
                    Encoding.UTF8
                );
            }
            catch
            {
                // ログの書き込みに失敗しても無視する
            }
        }
    }
}
