namespace Launcher.Config
{
    public sealed class LauncherConfig
    {
        /// <summary>ゲーム本体の設置フォルダ（例: "game"）。</summary>
        public string GameDirectory { get; set; }
        /// <summary>ゲーム実行ファイルのパス。相対なら GameDirectory 基準。未設定時は "Game.exe" を仮定。</summary>
        public string GameExePath { get; set; }
        /// <summary>現在バージョンを保持しているテキストのパス（例: "game\\version.txt"）。</summary>
        public string CurrentVersionFile { get; set; }
        /// <summary>最新マニフェスト latest.json の URL またはローカルパス。</summary>
        public string ManifestUrlOrPath { get; set; }

        public LauncherConfig()
        {
            GameDirectory = "game";
            GameExePath = "";
            CurrentVersionFile = "game\\version.txt";
            ManifestUrlOrPath = "";
        }
    }
}
