using System.IO;

namespace Launcher.Services.Executors
{
    /// <summary>
    /// RemoveFiles に基づいて不要ファイルやディレクトリを削除する。
    /// </summary>
    public sealed class PatchRemoveExecutor
    {
        private readonly Action<double, string> _progress;

        public PatchRemoveExecutor(Action<double, string> progress)
        {
            _progress = progress ?? ((_, __) => { });
        }

        public void Execute(string baseDir, List<string>? removeFiles)
        {
            if (removeFiles == null || removeFiles.Count == 0)
                return;

            _progress(75, "不要ファイルを削除しています...");

            foreach (var relPath in removeFiles)
            {
                string targetPath = Path.Combine(baseDir, "Game", relPath);
                try
                {
                    if (File.Exists(targetPath))
                    {
                        File.Delete(targetPath);
                    }
                    else if (Directory.Exists(targetPath))
                    {
                        Directory.Delete(targetPath, true);
                    }
                }
                catch (Exception ex)
                {
                    _progress(75, $"削除に失敗しました: {relPath} ({ex.Message})");
                }
            }
        }
    }
}
