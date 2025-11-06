using Launcher.Config;
using Launcher.Services;
using Launcher.Utility;
using System.Threading.Tasks;
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
                Error("設定ファイルの読み込みに失敗しました。", ex);

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

                string fullPath = System.IO.Path.GetFullPath(
                    System.IO.Path.Combine(AppContext.BaseDirectory, gameExePath)
                );
                if (_gameService.IsRunning(fullPath))
                {
                    Log("すでにゲームが起動中です。");
                    return;
                }

                var success = _gameService.Launch(fullPath);
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
                Error("ゲーム起動中にエラーが発生しました。", ex);
            }
        }

        /// <summary>
        /// 更新ボタン押下時の処理
        /// </summary>
        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // ゲームが起動中なら更新をキャンセル
                if (_gameService.IsRunning(_config.GameExePath))
                {
                    Log("ゲームが起動中のため、更新を中止します。");
                    return;
                }

                UpdateButton.IsEnabled = false;
                PlayButton.IsEnabled = false;
                ProgressBar.Value = 0;

                var updateService = new UpdateService();
                updateService.ProgressChanged += (progress, message) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ProgressBar.Value = progress;
                        Log(message);
                    });
                };

                await updateService.RunAsync(_config.ManifestUrl, "Game/version.txt");
            }
            catch (Exception ex)
            {
                Log("更新中にエラーが発生しました: " + ex.Message);
            }
            finally
            {
                UpdateButton.IsEnabled = true;
                PlayButton.IsEnabled = true;
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
                Close();
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

        private void Error(string message, Exception? exeption = null)
        {
            Log(message);
            Utility.Log.Error(message, exeption);
        }
    }
}