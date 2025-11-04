using Launcher.Model;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;

namespace Launcher.Services
{
    /// <summary>
    /// アップデート処理を管理するサービス。
    /// </summary>
    public sealed class UpdateService
    {
        public event Action<double, string>? ProgressChanged;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };
        private string TmpDir => Path.Combine(AppContext.BaseDirectory, "temp");

        public UpdateService()
        {
            var handler = new HttpClientHandler
            {
                AllowAutoRedirect = true
            };

            _httpClient = new HttpClient(handler);
            // User-Agent ヘッダーを設定
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Launcher/1.0");
            // JSONを扱うことを明示
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        }

        /// <summary>
        /// 最新マニフェストを取得し、バージョンを比較して更新が必要なら適用します。
        /// </summary>
        public async Task RunAsync(string manifestUrl, string versionFilePath)
        {
            Info(0, "最新バージョンを確認中...");

            // マニフェストの取得
            Manifest manifest = await GetManifestAsync(manifestUrl);

            // 差分パッチが存在しない（初期リリース）の場合はスキップ
            if (manifest.IsEmpty())
            {
                Info(100, "更新は存在しません。");
                return;
            }

            // バージョン情報の取得
            VersionInfo versionInfo = await CheckVersionAsync(manifest, versionFilePath);

            // バージョン比較
            if (!versionInfo.UpdateRequired)
            {
                Info(100, $"最新バージョンです({versionInfo.LatestVersion})。");
                return;
            }

            // 更新が必要な場合
            Info(10, $"更新を開始します: {versionInfo.CurrentVersion} → {versionInfo.LatestVersion}");

            try
            {
                string? patchUrl = manifest.GetPatchUrl();
                if (string.IsNullOrWhiteSpace(patchUrl))
                {
                    Info(100, "差分パッチは存在しません。");
                    return;
                }

                // パッチのダウンロード
                string zipPath = await DownloadPatchAsync(patchUrl ?? "");

                // ZIP展開・適用処理
                await ExtractAndApplyPatchAsync(zipPath);

                // バージョンファイル更新
                await File.WriteAllTextAsync(versionFilePath, versionInfo.LatestVersion);
            }
            finally
            {
                CleanupTempFiles();
            }

            Info(100, $"更新完了（{versionInfo.LatestVersion}）。");
        }

        private async Task<Manifest> GetManifestAsync(string manifestUrl)
        {
            string json;
            try
            {
                json = await _httpClient.GetStringAsync(manifestUrl);
            }
            catch (HttpRequestException ex)
            {
                Error("マニフェストの取得に失敗しました: " + ex.Message);
                throw new IOException($"マニフェストの取得に失敗しました。", ex);
            }

            Manifest? manifest = JsonSerializer.Deserialize<Manifest>(json, _jsonOptions);
            if (manifest == null)
            {
                Error("マニフェストの内容が不正です。");
                throw new InvalidDataException("マニフェストの内容が不正です。");
            }

            return manifest;
        }

        private async Task<VersionInfo> CheckVersionAsync(Manifest manifest, string versionFilePath)
        {
            // 現在バージョンを取得
            string currentVersion = "0.0.0";
            if (File.Exists(versionFilePath))
            {
                currentVersion = await File.ReadAllTextAsync(versionFilePath);
                currentVersion = currentVersion.Trim();
            }

            bool updateRequired = manifest.Version != currentVersion;

            return new VersionInfo(currentVersion, manifest.Version, updateRequired);
        }

        private async Task<string> DownloadPatchAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                Error("パッチファイルのURLが設定されていません。");
                throw new InvalidOperationException("パッチファイルのURLが設定されていません。");
            }

            string tmpDir = TmpDir;
            // 一度削除してから作成
            if (Directory.Exists(tmpDir))
            {
                Directory.Delete(tmpDir, true);
            }
            Directory.CreateDirectory(tmpDir);

            string outputPath = Path.Combine(tmpDir, "update.zip");
            Info(20, "パッチファイルをダウンロードしています...");

            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long total = response.Content.Headers.ContentLength ?? -1L;
                bool canReport = total > 0;

                await using var stream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = File.Create(outputPath);

                var buffer = new byte[81920];
                long totalRead = 0;
                int read;
                double lastPercent = 0;

                while ((read = await stream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read));
                    totalRead += read;

                    if (canReport)
                    {
                        double percent = 20 + (double)totalRead / total * 60;
                        if (percent - lastPercent >= 1)
                        {
                            Info(percent, $"ダウンロード中... {percent:F0}%");
                            lastPercent = percent;
                        }
                    }
                }

                Info(80, "パッチのダウンロードが完了しました。");
            }
            catch (Exception ex)
            {
                Error($"パッチのダウンロードに失敗しました: {ex.Message}");
                throw new IOException("パッチのダウンロードに失敗しました。", ex);
            }

            return outputPath;
        }

        private async Task ExtractAndApplyPatchAsync(string zipPath)
        {
            if (!File.Exists(zipPath))
            {
                Error("パッチファイルが見つかりません。");
                throw new FileNotFoundException("パッチファイルが見つかりません。", zipPath);
            }

            string baseDir = AppContext.BaseDirectory;
            string gameDir = Path.Combine(baseDir, "Game");
            string tempDir = Path.Combine(baseDir, "Game_temp");
            string oldDir = Path.Combine(baseDir, "Game_old");

            try
            {
                // クリーンアップ
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
                if (!Directory.Exists(gameDir))
                {
                    Directory.CreateDirectory(gameDir);
                }

                Info(70, "ゲームデータをコピーしています...");
                await CopyDirectoryAsync(gameDir, tempDir);

                Info(80, "パッチを展開しています..");
                string extractDir = Path.Combine(TmpDir, "extracted");
                if (Directory.Exists(extractDir))
                {
                    Directory.Delete(extractDir, true);
                }
                Directory.CreateDirectory(extractDir);
                ZipFile.ExtractToDirectory(zipPath, extractDir, true);

                Info(90, "パッチを適用しています...");
                await CopyDirectoryAsync(extractDir, tempDir);

                Info(95, "更新内容を反映しています...");
                // 旧フォルダ削除
                if (Directory.Exists(oldDir))
                {
                    Directory.Delete(oldDir, true);
                }

                // 元のフォルダを退避
                if (Directory.Exists(gameDir))
                {
                    Directory.Move(gameDir, oldDir);
                }

                // 新しいフォルダを正式フォルダにリネーム
                Directory.Move(tempDir, gameDir);

                Info(100, "更新が完了しました。旧データを削除します。");

                // 旧フォルダ削除
                if (Directory.Exists(oldDir))
                {
                    Directory.Delete(oldDir, true);
                }
            }
            catch (Exception ex)
            {
                Error($"パッチの適用に失敗しました: {ex.Message}");

                // ロールバック
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }

                throw;
            }
        }

        private static async Task CopyDirectoryAsync(string sourceDir, string destDir)
        {
            foreach (string dir in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                string targetDir = dir.Replace(sourceDir, destDir);
                Directory.CreateDirectory(targetDir);
            }

            foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string targetFile = file.Replace(sourceDir, destDir);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);

                await using var sourceStream = File.OpenRead(file);
                await using var destStream = File.Create(targetFile);
                await sourceStream.CopyToAsync(destStream);
            }
        }

        private void CleanupTempFiles()
        {
            string tmpDir = TmpDir;
            if (Directory.Exists(tmpDir))
            {
                try
                {
                    Directory.Delete(tmpDir, true);
                }
                catch
                {
                    // ファイルロック中などで削除できない場合は無視
                }
            }
        }

        private void Info(double percent, string message)
        {
            Progress(percent, message);
        }

        private void Error(string message)
        {
            Progress(0, "[Error] " + message);
        }

        private void Progress(double percent, string message)
        {
            ProgressChanged?.Invoke(percent, message);
        }
    }
}
