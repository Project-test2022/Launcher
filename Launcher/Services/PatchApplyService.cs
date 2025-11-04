using Launcher.Utility;
using System.IO;
using System.IO.Compression;

namespace Launcher.Services
{
    public sealed class PatchApplyService
    {
        private readonly Action<double, string> _progress;

        public PatchApplyService(Action<double, string> progress)
        {
            _progress = progress ?? ((_, __) => { });
        }

        /// <summary>
        /// ZIPを展開し、指定された削除リストを反映してパッチを適用します。
        /// </summary>
        public async Task ApplyAsync(string zipPath, List<string>? removeFiles = null)
        {
            if (!File.Exists(zipPath))
            {
                throw new FileNotFoundException("パッチファイルが見つかりません。", zipPath);
            }

            string baseDir = AppContext.BaseDirectory;
            string gameDir = Path.Combine(baseDir, "Game");
            string tempDir = Path.Combine(baseDir, "Game_temp");
            string oldDir = Path.Combine(baseDir, "Game_old");
            string extractDir = Path.Combine(baseDir, "temp", "extracted");

            try
            {
                // --- 一時ディレクトリ準備 ---
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                Directory.CreateDirectory(tempDir);
                Directory.CreateDirectory(extractDir);

                _progress(70, "パッチを展開しています...");
                ZipFile.ExtractToDirectory(zipPath, extractDir, true);

                // --- 削除ファイルの適用 ---
                if (removeFiles is { Count: > 0 })
                {
                    _progress(75, "不要ファイルを削除しています...");
                    foreach (var relPath in removeFiles)
                    {
                        string targetPath = Path.Combine(gameDir, relPath);
                        if (File.Exists(targetPath))
                        {
                            File.Delete(targetPath);
                        }
                        else if (Directory.Exists(targetPath))
                        {
                            Directory.Delete(targetPath, true);
                        }
                    }
                }

                _progress(80, "既存データをコピーしています...");
                await DirectoryUtility.CopyDirectoryAsync(gameDir, tempDir);

                _progress(90, "パッチ内容を適用しています...");
                await DirectoryUtility.CopyDirectoryAsync(extractDir, tempDir);

                _progress(95, "更新内容を反映しています...");
                if (Directory.Exists(oldDir)) Directory.Delete(oldDir, true);
                if (Directory.Exists(gameDir)) Directory.Move(gameDir, oldDir);
                Directory.Move(tempDir, gameDir);

                _progress(100, "更新完了。旧データを削除します。");
                if (Directory.Exists(oldDir)) Directory.Delete(oldDir, true);
            }
            catch (Exception)
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
                throw;
            }
        }
    }
}
