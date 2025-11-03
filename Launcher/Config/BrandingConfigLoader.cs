using System.IO;
using System.Text.Json;

namespace Launcher.Config
{
    public static class BrandingConfigLoader
    {
        public static BrandingConfig Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("ブランド設定ファイルが見つかりません。", path);
            }

            try
            {
                var json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<BrandingConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (config == null)
                {
                    throw new Exception("ブランド設定ファイルの内容が不正です。: " + path);
                }
                return config;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("ブランド設定ファイルの読み込みに失敗しました。: " + path, ex);
            }
        }
    }
}
