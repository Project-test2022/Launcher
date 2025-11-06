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
            List<AddArchiveEntry>? addFiles = null,
            List<PatchArchiveEntry>? patchArchives = null)
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

                // --- 差分ZIP展開 ---
                _progress(70, "差分パッチを展開しています...");
                ZipFile.ExtractToDirectory(patchZipPath, extractDir, true);

                // --- 不要ファイル削除 ---
                var remover = new PatchRemoveExecutor(_progress);
                remover.Execute(baseDir, removeFiles);

                // --- 差分適用 ---
                if (patchArchives is { Count: > 0 })
                {
                    _progress(80, "差分を適用しています...");
                    foreach (var archive in patchArchives)
                    {
                        if (archive.Files == null || archive.Files.Count == 0)
                            continue;

                        var deltaExecutor = new PatchDeltaExecutor(_progress);
                        await deltaExecutor.ExecuteAsync(baseDir, extractDir, archive.Files);
                    }
                }

                // --- 追加ファイル ---
                var adder = new PatchAddExecutor(_progress, _httpClient);
                await adder.ExecuteAsync(tmpRoot, gameDir, addFiles);

                // --- 完了処理 ---
                _progress(100, "更新が完了しました。");
            }
            catch
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                throw;
            }
            finally
            {
                // --- 後片付け ---
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, true);
                if (Directory.Exists(oldDir))
                    Directory.Delete(oldDir, true);
            }
        }
    }
}
