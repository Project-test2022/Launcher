using Launcher.Model;
using Launcher.Utility;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace Launcher.Services
{
    /// <summary>
    /// パッチ適用サービス。
    /// 差分パッチの展開、追加ファイルの適用、削除ファイルの反映を行う。
    /// </summary>
    public sealed class PatchApplyService
    {
        private readonly Action<double, string> _progress;
        private readonly HttpClient _httpClient;

        public PatchApplyService(Action<double, string> progress, HttpClient? httpClient = null)
        {
            _progress = progress ?? ((_, __) => { });
            _httpClient = httpClient ?? new HttpClient();
        }

        /// <summary>
        /// ZIPを展開し、削除・追加ファイルを反映してパッチを適用します。
        /// </summary>
        public async Task ApplyAsync(
            string patchZipPath,
            List<string>? removeFiles = null,
            List<AddArchiveEntry>? addFiles = null)
        {
            if (!File.Exists(patchZipPath))
                throw new FileNotFoundException("パッチファイルが見つかりません。", patchZipPath);

            string baseDir = AppContext.BaseDirectory;
            string gameDir = Path.Combine(baseDir, "Game");
            string tempDir = Path.Combine(baseDir, "Game_temp");
            string oldDir = Path.Combine(baseDir, "Game_old");
            string tmpRoot = Path.Combine(baseDir, "temp");
            string extractDir = Path.Combine(tmpRoot, "extracted");

            try
            {
                // --- 初期化 ---
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                Directory.CreateDirectory(tempDir);
                Directory.CreateDirectory(extractDir);

                _progress(70, "差分パッチを展開しています...");
                ZipFile.ExtractToDirectory(patchZipPath, extractDir, true);

                // --- 削除ファイルの処理 ---
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

                // --- 既存データコピー ---
                _progress(80, "既存データをコピーしています...");
                await DirectoryUtility.CopyDirectoryAsync(gameDir, tempDir);

                // --- 差分内容の適用 ---
                _progress(90, "差分を適用しています...");
                await DirectoryUtility.CopyDirectoryAsync(extractDir, tempDir);

                // --- 追加ファイルの展開 ---
                if (addFiles is { Count: > 0 })
                {
                    _progress(92, "追加ファイルを適用しています...");

                    foreach (var addEntry in addFiles)
                    {
                        // ZIPファイルをダウンロード
                        string addZipPath = Path.Combine(tmpRoot, addEntry.ArchiveName);
                        using (var response = await _httpClient.GetAsync(addEntry.Url))
                        {
                            response.EnsureSuccessStatusCode();
                            await using var stream = await response.Content.ReadAsStreamAsync();
                            await using var fs = File.Create(addZipPath);
                            await stream.CopyToAsync(fs);
                        }

                        // 展開
                        string addExtractDir = Path.Combine(tmpRoot, "add_" + Path.GetFileNameWithoutExtension(addEntry.ArchiveName));
                        if (Directory.Exists(addExtractDir))
                            Directory.Delete(addExtractDir, true);

                        Directory.CreateDirectory(addExtractDir);
                        ZipFile.ExtractToDirectory(addZipPath, addExtractDir, true);

                        // 展開したファイルを配置
                        foreach (var file in addEntry.Entries)
                        {
                            string sourcePath = Path.Combine(addExtractDir, file.ZipPath);
                            string destPath = Path.Combine(tempDir, file.TargetPath);
                            Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                            File.Copy(sourcePath, destPath, true);
                        }
                    }
                }

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
