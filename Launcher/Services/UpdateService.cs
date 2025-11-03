using Launcher.Config;
using Launcher.Model;
using Launcher.Utility;
using Octodiff.Core;
using Octodiff.Diagnostics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Launcher.Services
{
    public sealed class UpdateService
    {
        private readonly Action<string> _log;
        private readonly Action<double, string> _progress;

        public UpdateService(Action<string> log, Action<double, string> progress)
        {
            _log = log ?? (_ => { });
            _progress = progress ?? ((_, __) => { });
        }

        public async Task<bool> RunAsync(LauncherConfig config)
        {
            try
            {
                if (config == null)
                {
                    throw new ArgumentNullException(nameof(config));
                }
                if (string.IsNullOrWhiteSpace(config.ManifestUrlOrPath))
                {
                    throw new InvalidDataException("ManifestUrlOnPath が設定されていません。");
                }

                Directory.CreateDirectory(config.GameDirectory);

                var currentVersion = LoadCurrentVersion(config.CurrentVersionFile);
                _log("現在のバージョン: " + (string.IsNullOrWhiteSpace(currentVersion) ? "(不明)" : currentVersion));

                var manifest = LoadManifest(config.ManifestUrlOrPath);
                _log("最新: " + manifest.Version + "（基準: " + manifest.BaseFrom + "）");

                if (string.IsNullOrWhiteSpace(currentVersion))
                {
                    _log("[Error] 現在のバージョンが不明です。");
                    return false;
                }

                if (!string.Equals(currentVersion, manifest.BaseFrom, StringComparison.OrdinalIgnoreCase))
                {
                    _log("[ERROR] 現在バージョン(" + currentVersion + ") と基準(" + manifest.BaseFrom + ") が一致しません。");
                    return false;
                }

                // ざっくり件数ベースの全体進捗
                var total = manifest.Patches.Count == 0 ? 1 : manifest.Patches.Count;
                var done = 0;
                var backupDir = Path.Combine(config.GameDirectory, ".backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(backupDir);

                foreach (var patch in manifest.Patches)
                {
                    _progress(-1, "適用中: " + patch.Path);

                    var targetPath = Path.Combine(config.GameDirectory, patch.Path.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(targetPath))
                    {
                        _log("[ERROR] 対象ファイルが存在しません: " + targetPath);
                        return false;
                    }

                    // 旧ハッシュ検証
                    var baseHash = Hash.Sha256(targetPath);
                    if (!string.Equals(baseHash, patch.BaseSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        _log("[Error] 旧ハッシュ不一致: " + targetPath);
                        return false;
                    }

                    // パッチ取得（URL or ローカル）
                    var deltaTempPath = Path.Combine(Path.GetTempPath(), "delta_" + Guid.NewGuid().ToString("N") + ".octodelta");
                    if (IsHttpOrHttps(patch.Url))
                    {
                        using (var wc = new WebClient())
                        {
                            wc.DownloadProgressChanged += (s, e) =>
                            {
                                var percent = e.TotalBytesToReceive > 0 ? (double)e.BytesReceived / e.TotalBytesToReceive * 100.0 : 0.0;
                                _progress(Math.Min(percent, 100.0) * 0.5, "ダウンロード中: " + patch.Path);
                            };
                            await wc.DownloadFileTaskAsync(new Uri(patch.Url), deltaTempPath);
                        }
                    }
                    else
                    {
                        File.Copy(patch.Url, deltaTempPath, true);
                    }

                    // 差分適用（Octodiff）
                    var newTmpPath = Path.Combine(Path.GetTempPath(), "new_" + Guid.NewGuid().ToString("N"));
                    using (var basisStream = File.OpenRead(targetPath))
                    using (var deltaStream = File.OpenRead(deltaTempPath))
                    using (var newFileStream = File.Create(newTmpPath))
                    {
                        var progress = new ConsoleProgressReporter();
                        var deltaReader = new BinaryDeltaReader(deltaStream, progress);
                        var applier = new DeltaApplier();
                        applier.Apply(basisStream, deltaReader, newFileStream);
                    }

                    // 新ハッシュ検証
                    var newHash = Hash.Sha256(newTmpPath);
                    if (!string.Equals(newHash, patch.NewSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        _log("[Error] 新ハッシュ不一致: " + patch.Path);
                        return false;
                    }

                    // バックアップ作成
                    var backupPath = Path.Combine(backupDir, patch.Path.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(backupPath));
                    File.Replace(newTmpPath, targetPath, backupPath, true);

                    // 一時ファイル削除はベストエフォート
                    try { File.Delete(deltaTempPath); } catch { }

                    // 件数ベースの進捗
                    done++;
                    var percentAll = (double)done / total * 100.0;
                    _progress(percentAll, "適用済み: " + done + " / " + total);
                }

                // すべて成功 → バージョン更新
                File.WriteAllText(config.CurrentVersionFile, manifest.Version, Encoding.UTF8);
                _log("更新完了: " + manifest.Version);
                return true;
            }
            catch (Exception ex)
            {
                _log("[Error] " + ex.Message);

                Log.Error(ex.Message);
                return false;
            }
        }

        private static string LoadCurrentVersion(string path)
        {
            if (!File.Exists(path))
            {
                return "";
            }
            return File.ReadAllText(path).Trim();
        }

        private static bool IsHttpOrHttps(string url)
        {
            return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        private static Manifest LoadManifest(string urlOrPath)
        {
            if (IsHttpOrHttps(urlOrPath))
            {
                using (var wc = new WebClient())
                {
                    var json = wc.DownloadString(urlOrPath);
                    return JsonSerializer.Deserialize<Manifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            else
            {
                var json = File.ReadAllText(urlOrPath);
                return JsonSerializer.Deserialize<Manifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
        }
    }
}
