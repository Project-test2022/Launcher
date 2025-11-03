using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Launcher.Services
{
    /// <summary>
    /// アップデート処理を管理するサービス。
    /// </summary>
    public sealed class UpdateService
    {
        public event Action<double, string>? ProgressChanged;
        private readonly HttpClient _httpClient;

        public UpdateService()
        {
            _httpClient = new HttpClient();

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
            Progress(0, "最新バージョンを確認中...");

            if (string.IsNullOrWhiteSpace(manifestUrl))
            {
                throw new ArgumentException("マニフェストURLが指定されていません。", nameof(manifestUrl));
            }

            // 現在バージョンを取得
            var currentVersion = "0.0.0";
            if (File.Exists(versionFilePath))
            {
                currentVersion = await File.ReadAllTextAsync(versionFilePath);
                currentVersion = currentVersion.Trim();
            }

            // 最新マニフェストの取得
            string json;
            try
            {
                json = await _httpClient.GetStringAsync(manifestUrl);
            }
            catch (Exception ex)
            {
                Progress(0, "マニフェストの取得に失敗しました。");
                throw new IOException("マニフェストの取得に失敗しました。", ex);
            }

            // JSONパース
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (Exception ex)
            {
                Progress(0, "マニフェストの解析に失敗しました。");
                throw new InvalidDataException("マニフェストが不正です。", ex);
            }

            // versionフィールド取得
            if (!doc.RootElement.TryGetProperty("version", out var versionElement))
            {
                Progress(0, "マニフェストに version が含まれていません。");
                throw new InvalidDataException("マニフェストに version が含まれていません。");
            }

            var latestVersion = versionElement.GetString() ?? "0.0.0";

            // バージョン比較
            if (latestVersion == currentVersion)
            {
                Progress(100, $"最新バージョンです({currentVersion})。");
                return;
            }

            // 更新が必要な場合
            Progress(10, $"更新を開始します: {currentVersion} → {latestVersion}");

            // TODO: 差分ZIPをダウンロードして適用

            // バージョンファイル更新
            await File.WriteAllTextAsync(versionFilePath, latestVersion);
            Progress(100, $"更新完了（{latestVersion}）。");
        }

        private void Progress(double percent, string message)
        {
            ProgressChanged?.Invoke(percent, message);
        }
    }
}
