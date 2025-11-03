using Launcher.Services;
using System.Windows;
using System.Windows.Controls;

namespace Launcher
{
    public partial class MainWindow : Window
    {
        private readonly GameService _gameService;

        public MainWindow()
        {
            InitializeComponent();

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
                // TODO: 設定ファイル読み込み後に正しいパスを設定
                var gameExePath = "";

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