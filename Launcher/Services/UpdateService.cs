using Launcher.Model;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace Launcher.Services
{
    /// <summary>
    /// アップデート処理を管理するサービス。
    /// </summary>
    public sealed class UpdateService
    {
        public event Action<double, string>? ProgressChanged;

        private readonly ManifestFetchService _fetchService = new();
        private readonly VersionCheckService _versionService = new();

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
                    var downloader = new PatchDownloadService(_httpClient, Progress);
                    string zipPath = await downloader.DownloadAsync(patchUrl);

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
