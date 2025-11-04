using Launcher.Model;
using Launcher.Services.Executors;
using Launcher.Utility;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace Launcher.Services
{
    /// <summary>
    /// 差分ZIP・追加ファイル・削除ファイルを統合的に適用するサービス。
    /// </summary>
    public sealed class PatchApplyService
    {
        private readonly Action<double, string> _progress;
        private readonly HttpClient _httpClient;

        public PatchApplyService(Action<double, string> progress, HttpClient httpClient)
        {
            _progress = progress ?? ((_, __) => { });
            _httpClient = httpClient;
        }

        public async Task ApplyAsync(
            string patchZipPath,
            List<string>? removeFiles = null,
            List<AddArchiveEntry>? addFiles = null)
        {
            string baseDir = AppContext.BaseDirectory;
            string gameDir = Path.Combine(baseDir, "Game");
            string tempDir = Path.Combine(baseDir, "Game_temp");
            string oldDir = Path.Combine(baseDir, "Game_old");
            string tmpRoot = Path.Combine(baseDir, "temp");
            string extractDir = Path.Combine(tmpRoot, "extracted");

            try
            {
                // --- 準備 ---
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                Directory.CreateDirectory(tempDir);
                Directory.CreateDirectory(extractDir);

                _progress(70, "差分パッチを展開しています...");
                ZipFile.ExtractToDirectory(patchZipPath, extractDir, true);

                // --- 削除処理 ---
                var remover = new PatchRemoveExecutor(_progress);
                remover.Execute(baseDir, removeFiles);

                // --- 既存データコピー ---
                _progress(80, "既存データをコピーしています...");
                await DirectoryUtility.CopyDirectoryAsync(gameDir, tempDir);

                // --- 差分反映 ---
                _progress(90, "差分を適用しています...");
                await DirectoryUtility.CopyDirectoryAsync(extractDir, tempDir);

                // --- 追加ファイル処理 ---
                var adder = new PatchAddExecutor(_progress, _httpClient);
                await adder.ExecuteAsync(tmpRoot, tempDir, addFiles);

                // --- 本番反映 ---
                _progress(95, "更新内容を反映しています...");
                if (Directory.Exists(oldDir)) Directory.Delete(oldDir, true);
                if (Directory.Exists(gameDir)) Directory.Move(gameDir, oldDir);
                Directory.Move(tempDir, gameDir);

                _progress(100, "更新完了。旧データを削除しています...");
                if (Directory.Exists(oldDir)) Directory.Delete(oldDir, true);
            }
            catch
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                throw;
            }
        }
    }
}
