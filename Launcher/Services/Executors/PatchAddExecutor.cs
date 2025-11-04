using Launcher.Model;
using System.IO;
using System.IO.Compression;
using System.Net.Http;

namespace Launcher.Services.Executors
{
    /// <summary>
    /// AddFiles に基づいて追加ファイルをダウンロード・展開・配置する。
    /// </summary>
    public sealed class PatchAddExecutor
    {
        private readonly Action<double, string> _progress;
        private readonly HttpClient _httpClient;

        public PatchAddExecutor(Action<double, string> progress, HttpClient httpClient)
        {
            _progress = progress ?? ((_, __) => { });
            _httpClient = httpClient;
        }

        public async Task ExecuteAsync(string tempRoot, string targetDir, List<AddArchiveEntry>? addFiles)
        {
            if (addFiles == null || addFiles.Count == 0)
                return;

            _progress(92, "追加ファイルを適用しています...");

            foreach (var addEntry in addFiles)
            {
                string addZipPath = Path.Combine(tempRoot, addEntry.ArchiveName);

                // --- ZIPダウンロード ---
                using (var response = await _httpClient.GetAsync(addEntry.Url))
                {
                    response.EnsureSuccessStatusCode();
                    await using var stream = await response.Content.ReadAsStreamAsync();
                    await using var fs = File.Create(addZipPath);
                    await stream.CopyToAsync(fs);
                }

                // --- 展開 ---
                string extractDir = Path.Combine(tempRoot, "add_" + Path.GetFileNameWithoutExtension(addEntry.ArchiveName));
                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, true);
                Directory.CreateDirectory(extractDir);

                ZipFile.ExtractToDirectory(addZipPath, extractDir, true);

                // --- 展開ファイルをコピー ---
                foreach (var file in addEntry.Entries)
                {
                    string sourcePath = Path.Combine(extractDir, file.ZipPath);
                    string destPath = Path.Combine(targetDir, file.TargetPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    File.Copy(sourcePath, destPath, true);
                }
            }
        }
    }
}
