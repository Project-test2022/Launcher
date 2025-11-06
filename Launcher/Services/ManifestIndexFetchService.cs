using Launcher.Model;
using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace Launcher.Services
{
    public sealed class ManifestIndexFetchService
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };

        public ManifestIndexFetchService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// manifest_index.jsonを取得します。
        /// </summary>
        public async Task<ReleaseIndex> FetchAsync(string indexUrl)
        {
            string json = await _httpClient.GetStringAsync(indexUrl);
            var index = JsonSerializer.Deserialize<ReleaseIndex>(json, _options);
            if (index == null || index.Releases == null || index.Releases.Count == 0)
            {
                throw new InvalidDataException("リリースインデックスの内容が不正です。");
            }
            return index;
        }
    }
}
