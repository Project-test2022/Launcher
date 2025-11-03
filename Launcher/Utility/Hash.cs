using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Launcher.Utility
{
    public static class Hash
    {
        /// <summary>
        /// ファイルのSHA-256ハッシュ値を16進数文字列で返します。
        /// </summary>
        /// <param name="file">対象ファイルパス</param>
        public static string Sha256(string file)
        {
            if (!File.Exists(file))
            {
                throw new FileNotFoundException("ファイルが見つかりません。", file);
            }

            using (var fs = File.OpenRead(file))
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(fs);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }
    }
}
