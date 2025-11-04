using Launcher.Model;
using System.IO;
using System.Net.Http;

namespace Launcher.Services
{
    /// <summary>
    /// アップデート処理を管理するサービス。
    /// </summary>
    public sealed class UpdateService
    {
        public event Action<double, string>? ProgressChanged;

        private readonly ManifestFetchService _fetchService;
        private readonly VersionCheckService _versionService;
        private readonly PatchDownloadService _downloadService;
        private readonly PatchApplyService _applyService;

        private readonly HttpClient _httpClient;
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

            _fetchService = new ManifestFetchService();
            _versionService = new VersionCheckService();
            _downloadService = new PatchDownloadService(_httpClient, Progress);
            _applyService = new PatchApplyService(Progress, _httpClient);
        }

        /// <summary>
        /// 最新マニフェストを取得し、バージョンを比較して更新が必要なら適用します。
        /// </summary>
        public async Task RunAsync(string manifestUrl, string versionFilePath)
        {
            try
            {
                Info(0, "最新バージョンを確認中...");

                // マニフェストの取得
                Manifest manifest = await _fetchService.FetchAsync(manifestUrl);
                // 差分パッチが存在しない（初期リリース）の場合はスキップ
                if (manifest.IsEmpty())
                {
                    Info(100, "更新は存在しません。");
                    return;
                }

                // バージョン情報の取得
                VersionInfo versionInfo = await _versionService.CheckAsync(manifest, versionFilePath);

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
                    string zipPath = await _downloadService.DownloadAsync(patchUrl);

                    // ZIP展開・適用処理
                    await _applyService.ApplyAsync(zipPath, manifest.RemoveFiles, manifest.AddFiles);

                    // バージョンファイル更新
                    await File.WriteAllTextAsync(versionFilePath, versionInfo.LatestVersion);
                }
                finally
                {
                    CleanupTempFiles();
                }

                Info(100, $"更新完了（{versionInfo.LatestVersion}）。");
            }
            catch (IOException ex)
            {
                Error("ネットワークまたはファイルアクセスでエラーが発生しました: " + ex.Message);
            }
            catch (InvalidDataException ex)
            {
                Error("マニフェストの内容が不正です: " + ex.Message);
            }
            catch (Exception ex)
            {
                Error("予期しないエラーが発生しました: " + ex.Message);
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
