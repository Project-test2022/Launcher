using System.IO;
using System.Net.Http;

namespace Launcher.Services
{
    /// <summary>
    /// パッチファイルのダウンロードを担当するサービス。
    /// </summary>
    public sealed class PatchDownloadService
    {
        private readonly HttpClient _httpClient;
        private readonly Action<double, string> _progress;
        private readonly string _tempDir;

        public PatchDownloadService(HttpClient httpClient, Action<double, string> progress)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _progress = progress ?? ((_, _) => { });

            _tempDir = Path.Combine(AppContext.BaseDirectory, "temp");
        }

        /// <summary>
        /// 指定されたURLからパッチをダウンロードし、保存パスを返します。
        /// </summary>
        public async Task<string> DownloadAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                throw new InvalidOperationException("パッチファイルのURLが指定されていません。");
            }

            // tempディレクトリをリセット
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
            Directory.CreateDirectory(_tempDir);

            string outputPath = Path.Combine(_tempDir, "update.zip");
            _progress(20, "パッチファイルをダウンロードしています...");

            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                long total = response.Content.Headers.ContentLength ?? -1L;
                bool canReport = total > 0;

                await using var stream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = File.Create(outputPath);

                byte[] buffer = new byte[81920];
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
                            _progress(percent, $"ダウンロード中... {percent:F0}%");
                            lastPercent = percent;
                        }
                    }
                }

                _progress(80, "パッチのダウンロードが完了しました。");
            }
            catch (Exception ex)
            {
                throw new IOException("パッチのダウンロードに失敗しました。", ex);
            }

            return outputPath;
        }
    }
}
