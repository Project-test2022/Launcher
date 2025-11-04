using Launcher.Model;
using Octodiff.Core;
using Octodiff.Diagnostics;
using System.IO;

namespace Launcher.Services.Executors
{
    /// <summary>
    /// Octodiffの差分ファイルを適用して既存ファイルを更新する。
    /// </summary>
    public sealed class PatchDeltaExecutor
    {
        private readonly Action<double, string> _progress;

        public PatchDeltaExecutor(Action<double, string> progress)
        {
            _progress = progress ?? ((_, __) => { });
        }

        /// <summary>
        /// 差分を適用して新しいファイルを再構築する。
        /// </summary>
        public async Task ExecuteAsync(string baseDir, string extractDir, List<PatchFileEntry> files)
        {
            if (files == null || files.Count == 0)
                return;

            _progress(85, "差分ファイルを適用しています...");

            foreach (var file in files.Where(f => !f.IsAdded && !f.IsRemoved))
            {
                string originalPath = Path.Combine(baseDir, "Game", file.Path);
                string deltaPath = Path.Combine(extractDir, file.Delta ?? "");
                string tempPath = originalPath + ".tmp";

                if (!File.Exists(originalPath))
                {
                    _progress(85, $"元ファイルが見つかりません: {file.Path}");
                    continue;
                }
                if (!File.Exists(deltaPath))
                {
                    _progress(85, $"差分ファイルが見つかりません: {file.Delta}");
                    continue;
                }

                try
                {
                    using (var basisStream = File.OpenRead(originalPath))
                    using (var deltaStream = File.OpenRead(deltaPath))
                    using (var outputStream = File.Create(tempPath))
                    {
                        var progress = new ConsoleProgressReporter();
                        var deltaApplier = new DeltaApplier { SkipHashCheck = false };
                        deltaApplier.Apply(basisStream, new BinaryDeltaReader(deltaStream, progress), outputStream);
                    }

                    // 旧ファイルを置き換え
                    File.Delete(originalPath);
                    File.Move(tempPath, originalPath);
                }
                catch (Exception ex)
                {
                    _progress(85, $"差分適用失敗: {file.Path} ({ex.Message})");
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }

                await Task.Yield(); // UIのブロックを防ぐ
            }
        }
    }
}
