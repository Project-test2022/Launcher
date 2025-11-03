using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Launcher.Config
{
    public sealed class BrandingConfig
    {
        /// <summary>ゲームの表示名（ウィンドウタイトルや画面見出しに使用）。</summary>
        public string GameTitle { get; set; }
        /// <summary>ロゴ画像のパス（exeと同階層基準の相対または絶対パス）。</summary>
        public string LogoImagePath { get; set; }

        public BrandingConfig()
        {
            GameTitle = "Game";
            LogoImagePath = "";
        }
    }
}
