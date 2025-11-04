using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Launcher.Utility
{
    /// <summary>
    /// ディレクトリ操作を行うユーティリティクラス。
    /// </summary>
    public static class DirectoryUtility
    {
        /// <summary>
        /// ディレクトリ全体を非同期でコピーします。
        /// </summary>
        public static async Task CopyDirectoryAsync(string sourceDir, string destDir)
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

        /// <summary>
        /// ディレクトリを安全に削除します（ロック中は無視）。
        /// </summary>
        public static void SafeDelete(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, true);
            }
            catch
            {
                // 無視
            }
        }
    }
}
