using Launcher.Model;
using System.IO;
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
            var manifest = await GetManifestAsync(manifestUrl);

            // バージョン情報の取得
            var versionInfo = await CheckVersionAsync(manifest, versionFilePath);

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
                // パッチのダウンロード
                var zipPath = await DownloadPatchAsync(manifest.PatchUrl ?? "");

                // TODO: ZIP展開・適用処理

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

            var manifest = JsonSerializer.Deserialize<Manifest>(json, _jsonOptions);
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
            var currentVersion = "0.0.0";
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

            var tmpDir = TmpDir;
            // 一度削除してから作成
            if (Directory.Exists(tmpDir))
            {
                Directory.Delete(tmpDir, true);
            }
            Directory.CreateDirectory(tmpDir);

            var outputPath = Path.Combine(tmpDir, "update.zip");
            Info(20, "パッチファイルをダウンロードしています...");

            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? -1L;
                var canReport = total > 0;

                await using var stream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = File.Create(outputPath);

                var buffer = new byte[81920];
                long totalRead = 0;
                int read;
                double latsPercent = 0;

                while ((read = await stream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read));
                    totalRead += read;

                    if (canReport)
                    {
                        var percent = 20 + (double)totalRead / total * 60;
                        if (percent - latsPercent >= 1)
                        {
                            Info(percent, $"ダウンロード中... {percent:F0}%");
                            latsPercent = percent;
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

        private void CleanupTempFiles()
        {
            var tmpDir = TmpDir;
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
