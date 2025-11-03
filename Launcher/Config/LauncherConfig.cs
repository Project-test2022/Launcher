using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Launcher.Config
{
    /// <summary>
    /// ランチャーの基本設定を表すクラス。
    /// </summary>
    public sealed class LauncherConfig
    {
        /// <summary>
        /// ゲームタイトル（UI表示用）
        /// </summary>
        public required string GameTitle { get; init; }

        /// <summary>
        /// ゲーム実行ファイルのパス
        /// </summary>
        public required string GameExePath { get; init; }

        /// <summary>
        /// マニフェスト（latest.json）のURLまたはパス
        /// </summary>
        public required string ManifestUrl { get; init; }

        public static LauncherConfig Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"設定ファイルが見つかりません: {path}");
            }

            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var config = JsonSerializer.Deserialize<LauncherConfig>(json, options);
            if (config == null)
            {
                throw new InvalidDataException("設定ファイルの内容を読み込めませんでした。");
            }

            // 内容の検証
            if (string.IsNullOrWhiteSpace(config.GameExePath))
            {
                throw new InvalidDataException("設定ファイルに GameExePath が指定されていません。");
            }

            if (string.IsNullOrWhiteSpace(config.GameTitle))
            {
                throw new InvalidDataException("設定ファイルに GameTitle が指定されていません。");
            }

            if (string.IsNullOrWhiteSpace(config.ManifestUrl))
            {
                throw new InvalidDataException("設定ファイルに ManifestUrl が指定されていません。");
            }

            return config;
        }
    }
}
