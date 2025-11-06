using Launcher.Model;
using System.IO;
using System.Net.Http;

namespace Launcher.Services
{
    /// <summary>
    /// 段階的アップデートを管理するサービス。
    /// </summary>
    public sealed class UpdateService
    {
        public event Action<double, string>? ProgressChanged;

        private readonly ManifestIndexFetchService _indexFetchService;
        private readonly ManifestFetchService _manifestFetchService;
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
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Launcher/1.0");
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

            _indexFetchService = new ManifestIndexFetchService(_httpClient);
            _manifestFetchService = new ManifestFetchService();
            _downloadService = new PatchDownloadService(_httpClient, Progress);
            _applyService = new PatchApplyService(Progress, _httpClient);
        }

        /// <summary>
        /// 段階的にすべてのアップデートを適用します。
        /// </summary>
        public async Task RunAsync(string manifestIndexUrl, string versionFilePath)
        {
            try
            {
                Info(0, "リリースインデックスを取得しています...");

                // manifest_index.jsonの取得
                ReleaseIndex releaseIndex = await _indexFetchService.FetchAsync(manifestIndexUrl);

                // 現在バージョンを取得
                string currentVersion = "0.0.0";
                if (File.Exists(Path.Combine(AppContext.BaseDirectory, versionFilePath)))
                {
                    currentVersion = (await File.ReadAllTextAsync(versionFilePath)).Trim();
                }

                // 段階的にアップデートを適用
                bool updated = false;

                while (true)
                {
                    // 現在バージョンをBaseFromとして持つリリースを探す
                    ReleaseEntry? nextRelease = releaseIndex.Releases
                        .FirstOrDefault(r => r.BaseFrom == currentVersion);

                    if (nextRelease == null)
                    {
                        if (!updated)
                            Info(100, $"最新バージョンです ({currentVersion})。");
                        else
                            Info(100, $"すべての更新を適用しました ({currentVersion})。");
                        break;
                    }

                    Info(5, $"次のバージョン {nextRelease.Version} を確認中...");

                    // latest.json取得
                    Manifest manifest = await _manifestFetchService.FetchAsync(nextRelease.Url);

                    if (manifest.IsEmpty())
                    {
                        Info(100, $"更新情報が存在しません ({nextRelease.Version})。");
                        break;
                    }

                    // パッチURL取得
                    string? patchUrl = manifest.GetPatchUrl();
                    if (string.IsNullOrWhiteSpace(patchUrl))
                    {
                        Info(100, $"パッチファイルが存在しません ({nextRelease.Version})。");
                        break;
                    }

                    Info(10, $"更新を開始します: {currentVersion} → {nextRelease.Version}");

                    // パッチダウンロード
                    string zipPath = await _downloadService.DownloadAsync(patchUrl);

                    // パッチ適用
                    await _applyService.ApplyAsync(zipPath, manifest.RemoveFiles, manifest.AddFiles, manifest.PatchArchives);

                    // バージョンファイル更新
                    await File.WriteAllTextAsync(versionFilePath, nextRelease.Version);
                    currentVersion = nextRelease.Version;
                    updated = true;

                    Info(90, $"バージョン {currentVersion} への更新が完了しました。");
                }
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
            finally
            {
                CleanupTempFiles();
            }
        }

        private void CleanupTempFiles()
        {
            string tmpDir = TmpDir;
            if (Directory.Exists(tmpDir))
            {
                try { Directory.Delete(tmpDir, true); } catch { }
            }
        }

        private void Info(double percent, string message) => Progress(percent, message);
        private void Error(string message) => Progress(0, "[Error] " + message);
        private void Progress(double percent, string message) => ProgressChanged?.Invoke(percent, message);
    }
}
