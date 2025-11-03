using System.Diagnostics;
using System.IO;

namespace Launcher.Services
{
    /// <summary>
    /// ゲームの起動と実行状態を管理するサービス。
    /// </summary>
    public sealed class GameService
    {
        private Process? _gameProcess;

        /// <summary>
        /// ゲーム起動時に発生するイベント。
        /// </summary>
        public event Action? GameStarted;

        /// <summary>
        /// ゲーム終了時に発生するイベント。
        /// </summary>
        public event Action? GameExited;

        /// <summary>
        /// ゲームがすでに実行中かどうかを確認します。
        /// </summary>
        public bool IsRunning(string gameExePath)
        {
            if (string.IsNullOrWhiteSpace(gameExePath))
            {
                return false;
            }

            var processName = Path.GetFileNameWithoutExtension(gameExePath);
            var existing = Process.GetProcessesByName(processName);
            return existing.Length > 0;
        }

        public bool Laungh(string gameExePath)
        {
            if (string.IsNullOrWhiteSpace(gameExePath))
            {
                throw new ArgumentException("ゲーム実行ファイルのパスが指定されていません。", nameof(gameExePath));
            }

            if (!File.Exists(gameExePath))
            {
                throw new FileNotFoundException("ゲーム実行ファイルが見つかりません。", gameExePath);
            }

            if (IsRunning(gameExePath))
            {
                return false;
            }

            try
            {
                _gameProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = gameExePath,
                        WorkingDirectory = Path.GetDirectoryName(gameExePath) ?? Environment.CurrentDirectory,
                        UseShellExecute = true
                    },
                    EnableRaisingEvents = true
                };

                _gameProcess.Exited += (s, e) =>
                {
                    GameExited?.Invoke();
                    _gameProcess?.Dispose();
                    _gameProcess = null;
                };

                if (_gameProcess.Start())
                {
                    GameStarted?.Invoke();
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ゲームの起動に失敗しました: {ex.Message}");
                return false;
            }

            return false;
        }
    }
}
