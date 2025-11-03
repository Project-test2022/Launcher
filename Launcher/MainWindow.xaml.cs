using Launcher.Config;
using Launcher.Services;
using System.Windows;
using System.Windows.Controls;

namespace Launcher
{
    public partial class MainWindow : Window
    {
        private readonly GameService _gameService;
        private readonly LauncherConfig _config;

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                // 設定ファイルの読み込み
                _config = LauncherConfig.Load("launcher.json");

                // タイトル表示を設定ファイルから
                TitleText.Text = _config.GameTitle;

                Log("設定ファイルを読み込みました。");
            }
            catch (Exception ex)
            {
                Log("設定ファイルの読み込みに失敗しました: " + ex.Message);

                // 設定ファイルがない場合は操作不可
                PlayButton.IsEnabled = false;
                UpdateButton.IsEnabled = false;
                return;
            }

            _gameService = new GameService();
            _gameService.GameStarted += OnGameStarted;
            _gameService.GameExited += OnGameExited;
        }

        /// <summary>
        /// プレイボタン押下時の処理
        /// </summary>
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var gameExePath = _config.GameExePath;

                if (string.IsNullOrWhiteSpace(gameExePath))
                {
                    Log("ゲームのパスが指定されていません。");
                    return;
                }

                if (_gameService.IsRunning(gameExePath))
                {
                    Log("すでにゲームが起動中です。");
                    return;
                }

                var success = _gameService.Launch(gameExePath);
                if (!success)
                {
                    Log("ゲームの起動に失敗しました。");
                    return;
                }

                PlayButton.IsEnabled = false;
                UpdateButton.IsEnabled = false;
            }
            catch (Exception ex)
            {
                Log("ゲーム起動中にエラーが発生しました: " + ex.Message);
            }
        }

        /// <summary>
        /// 更新ボタン押下時の処理
        /// </summary>
        private void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            Log("更新ボタンが押されました。（未実装）");
        }

        /// <summary>
        /// ゲーム起動イベントハンドラ
        /// </summary>
        private void OnGameStarted()
        {
            Dispatcher.Invoke(() =>
            {
                Log("ゲームを起動しました。");
                ProgressBar.Value = 0;
            });
        }

        /// <summary>
        /// ゲーム終了イベントハンドラ
        /// </summary>
        private void OnGameExited()
        {
            Dispatcher.Invoke(() =>
            {
                Log("ゲームが終了しました。");
                PlayButton.IsEnabled = true;
                UpdateButton.IsEnabled = true;
            });
        }

        /// <summary>
        /// ログ出力
        /// </summary>
        private void Log(string message)
        {
            LogText.Text = $"[{DateTime.Now:HH:mm:ss}] {message}";
        }
    }
}