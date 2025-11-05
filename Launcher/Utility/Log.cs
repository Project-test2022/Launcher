using System;
using System.IO;
using System.Text;

namespace Launcher.Utility
{
    /// <summary>
    /// シンプルなログ出力クラス。
    /// </summary>
    public static class Log
    {
        private static readonly object _lock = new();
        private static readonly string _logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
        private static readonly string _logFilePath = Path.Combine(_logDirectory, "app.log");

        /// <summary>
        /// 情報ログを出力します。
        /// </summary>
        public static void Info(string message) => Write("INFO", message);

        /// <summary>
        /// 警告ログを出力します。
        /// </summary>
        public static void Warning(string message) => Write("WARNING", message);

        /// <summary>
        /// エラーログを出力します。
        /// </summary>
        public static void Error(string message, Exception? exception = null)
        {
            var text = message;
            if (exception != null)
            {
                text += Environment.NewLine + exception;
            }
            Write("ERROR", text);
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    if (!Directory.Exists(_logDirectory))
                    {
                        Directory.CreateDirectory(_logDirectory);
                    }

                    var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}][{level}] {message}";
                    File.AppendAllText(_logFilePath, logLine + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // ログ書き込みに失敗しても例外は投げない
            }
        }
    }
}
