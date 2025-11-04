using Launcher.Model;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace Launcher.Services
{
    /// <summary>
    /// マニフェストの取得を担当するサービス。
    /// </summary>
    public sealed class ManifestFetchService
    {
        public event Action<double, string>? ProgressChanged;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ManifestFetchService()
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

        public async Task<Manifest> FetchAsync(string manifestUrl)
        {
            if (string.IsNullOrWhiteSpace(manifestUrl))
            {
                throw new ArgumentException("マニフェストのURLが指定されていません。", nameof(manifestUrl));
            }

            try
            {
                var json = await _httpClient.GetStringAsync(manifestUrl);
                Manifest? manifest = JsonSerializer.Deserialize<Manifest>(json, _jsonOptions);

                if (manifest == null)
                {
                    throw new InvalidDataException("マニフェストの内容が不正です。");
                }

                return manifest;
            }
            catch (HttpRequestException ex)
            {
                throw new IOException($"マニフェストの取得に失敗しました。", ex);
            }
        }
    }
}
