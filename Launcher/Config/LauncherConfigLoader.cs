using System.IO;
using System.Text.Json;

namespace Launcher.Config
{
    public static class LauncherConfigLoader
    {
        public static LauncherConfig Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("設定ファイルが見つかりません。", path);
            }

            try
            {
                var json = File.ReadAllText(path);
                var config = JsonSerializer.Deserialize<LauncherConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (config == null)
                {
                    throw new Exception("設定ファイルの内容が不正です。: " + path);
                }

                return config;
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("設定ファイルの読み込みに失敗しました。: " + path, ex);
            }
        }
    }
}
