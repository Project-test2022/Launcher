using System.IO;
using System.IO.Compression;
using Launcher.Utility;

namespace Launcher.Services
{
    /// <summary>
    /// ダウンロード済みパッチの展開および適用を担当するサービス。
    /// </summary>
    public sealed class PatchApplyService
    {
        private readonly Action<double, string> _progress;
        private readonly string _baseDir;
        private readonly string _gameDir;
        private readonly string _tempDir;
        private readonly string _oldDir;
        private readonly string _extractDir;

        public PatchApplyService(Action<double, string> progress)
        {
            _progress = progress ?? ((_, _) => { });

            _baseDir = AppContext.BaseDirectory;
            _gameDir = Path.Combine(_baseDir, "Game");
            _tempDir = Path.Combine(_baseDir, "Game_temp");
            _oldDir = Path.Combine(_baseDir, "Game_old");
            _extractDir = Path.Combine(_baseDir, "temp", "extracted");
        }

        /// <summary>
        /// パッチZIPを展開し、既存ゲームフォルダに適用します。
        /// </summary>
        public async Task ApplyAsync(string zipPath)
        {
            if (!File.Exists(zipPath))
            {
                throw new FileNotFoundException("パッチファイルが見つかりません。", zipPath);
            }

            try
            {
                // 一時フォルダを初期化
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }

                if (!Directory.Exists(_gameDir))
                {
                    Directory.CreateDirectory(_gameDir);
                }

                _progress(70, "ゲームデータをコピーしています...");
                await DirectoryUtility.CopyDirectoryAsync(_gameDir, _tempDir);

                _progress(80, "パッチを展開しています...");
                if (Directory.Exists(_extractDir))
                {
                    Directory.Delete(_extractDir, true);
                }
                Directory.CreateDirectory(_extractDir);
                ZipFile.ExtractToDirectory(zipPath, _extractDir, true);

                _progress(90, "パッチを適用しています...");
                await DirectoryUtility.CopyDirectoryAsync(_extractDir, _tempDir);

                _progress(95, "更新内容を反映しています...");

                // 旧フォルダ削除
                if (Directory.Exists(_oldDir))
                {
                    Directory.Delete(_oldDir, true);
                }

                // 元のフォルダを退避
                if (Directory.Exists(_gameDir))
                {
                    Directory.Move(_gameDir, _oldDir);
                }

                // 新しいフォルダを正式なGameフォルダにリネーム
                Directory.Move(_tempDir, _gameDir);

                _progress(100, "更新が完了しました。旧データを削除します。");

                // 古いデータを削除
                if (Directory.Exists(_oldDir))
                {
                    Directory.Delete(_oldDir, true);
                }
            }
            catch (Exception ex)
            {
                _progress(0, "[Error] パッチ適用中にエラーが発生しました: " + ex.Message);

                // ロールバック処理
                if (Directory.Exists(_tempDir))
                {
                    Directory.Delete(_tempDir, true);
                }

                throw;
            }
        }
    }
}
