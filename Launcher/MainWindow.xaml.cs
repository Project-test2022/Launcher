using Launcher.Config;
using Launcher.Services;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Launcher
{
    public partial class MainWindow : Window
    {
        private LauncherConfig launcherConfig;
        private BrandingConfig brandingConfig;
        private readonly UpdateService updateService;

        public MainWindow()
        {
            InitializeComponent();

            updateService = new UpdateService(
                log => Dispatcher.Invoke(() => StatusText.Text = log),
                (percent, detail) => Dispatcher.Invoke(() =>
                {
                    ProgressBar.IsIndeterminate = percent < 0;
                    if (percent >= 0)
                    {
                        ProgressBar.Value = percent;
                    }
                    ProgressDetail.Text = detail ?? "";
                })
            );

            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 設定読み込み（exe と同階層の既定ファイル名を想定）
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var launcherSettingsPath = Path.Combine(baseDir, "launcher_settings.json");
                var brandingPath = Path.Combine(baseDir, "branding.json");

                launcherConfig = LauncherConfigLoader.Load(launcherSettingsPath);
                brandingConfig = BrandingConfigLoader.Load(brandingPath);

                ApplyBranding(baseDir, brandingConfig);
            }
            catch (Exception ex)
            {
                MessageBox.Show("設定ファイルの読み込みに失敗しました。\n" + ex.Message, "設定エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                ShowFailureAndStay("設定ファイルの読み込みに失敗しました。例外: " + ex.Message, "設定ファイルの読み込みに失敗したため実行できません");
                return;
            }

            // 起動中チェック
            if (IsGameRunning())
            {
                MessageBox.Show("ゲームが既に起動中です。先にゲームを終了してください。", "起動エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                ShowFailureAndStay("ゲームが既に起動中です。", "ゲームが既に起動中のため実行できません");
                return;
            }

            // 起動時に自動更新
            ProgressBar.IsIndeterminate = true;
            StatusText.Text = "更新を確認しています...";
            ProgressDetail.Text = "";

            var isUpdated = await TryRunUpdateOnceAsync();

            if (isUpdated)
            {
                // 更新が成功したら自動でアプリ起動して終了
                StartGameThenExit();
                return;
            }

            // エラー時のポップアップ
            var result = MessageBox.Show(
                "更新に失敗しました。更新せずにゲームを起動しますか？",
                "更新エラー",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                StartGameThenExit();
            }
            else
            {
                Close();
            }
        }

        private async Task<bool> TryRunUpdateOnceAsync()
        {
            try
            {
                var ok = await updateService.RunAsync(launcherConfig);
                if (ok)
                {
                    ProgressBar.IsIndeterminate = false;
                    ProgressBar.Value = 100;
                    StatusText.Text = "最新のバージョンです。";
                }
                else
                {
                    ProgressBar.IsIndeterminate = false;
                    StatusText.Text = "更新に失敗しました。";
                }
                return ok;
            }
            catch (Exception ex)
            {
                ProgressBar.IsIndeterminate = false;
                StatusText.Text = "更新中にエラーが発生しました。";
                MessageBox.Show("更新中にエラーが発生しました。\n" + ex.Message, "更新エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void ApplyBranding(string baseDirectory, BrandingConfig branding)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(branding.GameTitle))
                {
                    this.Title = branding.GameTitle + " Launcher";
                    GameTitleText.Text = branding.GameTitle;
                }

                if (!string.IsNullOrWhiteSpace(branding.LogoImagePath))
                {
                    var logoPath = branding.LogoImagePath;
                    if (!Path.IsPathRooted(logoPath))
                    {
                        logoPath = Path.Combine(baseDirectory, logoPath);
                    }

                    if (File.Exists(logoPath))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(logoPath);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        LogoImage.Source = bitmap;
                    }
                }
            }
            catch
            {
                // ブランディングの適用に失敗しても無視する
            }
        }

        private void StartGameThenExit()
        {
            try
            {
                var path = launcherConfig.GameExePath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    path = Path.Combine(launcherConfig.GameDirectory, "Game.exe");
                }
                else if (!Path.IsPathRooted(path))
                {
                    path = Path.Combine(launcherConfig.GameDirectory, path);
                }

                if (!File.Exists(path))
                {
                    MessageBox.Show("ゲームの実行ファイルが見つかりません。\n" + path, "起動エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    ShowFailureAndStay("ゲームの実行ファイルが見つかりません: " + path, "ゲームの実行ファイルが見つからないため起動できません");
                    return;
                }

                if (IsGameRunning())
                {
                    MessageBox.Show("ゲームが既に実行中のため、新たに起動できません。", "起動エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    ShowFailureAndStay("ゲームが既に実行中のため、新たに起動できません。", "ゲームが既に実行中のため起動できません");
                    return;
                }

                var startInfo = new ProcessStartInfo(path)
                {
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(path)
                };
                Process.Start(startInfo);
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("ゲームの起動に失敗しました。\n" + ex.Message, "起動エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                ShowFailureAndStay("ゲームの起動に失敗しました。例外: " + ex.Message, "ゲームの起動に失敗しました");
            }
        }

        private bool IsGameRunning()
        {
            try
            {
                var exePath = GetExpectedGameExePath();
                var processName = Path.GetFileNameWithoutExtension(exePath);
                if (string.IsNullOrWhiteSpace(processName))
                {
                    return false;
                }

                var processes = Process.GetProcessesByName(processName);
                foreach (var process in processes)
                {
                    try
                    {
                        var runningPath = process.MainModule?.FileName;
                        if (string.Equals(runningPath, exePath, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // アクセス拒否は名前だけで判定
                        return processes.Length > 0;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private string GetExpectedGameExePath()
        {
            var path = launcherConfig.GameExePath;

            // 設定に明示されていない場合は既定: GameDirectory/Game.exe
            if (string.IsNullOrWhiteSpace(path))
            {
                path = Path.Combine(launcherConfig.GameDirectory, "Game.exe");
            }
            // 相対パスなら GameDirectory を基準に解決
            else if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(launcherConfig.GameDirectory, path);
            }

            return path;
        }

        private void ShowFailureAndStay(string messageForUser, string statusLineForUi)
        {
            StatusText.Text = statusLineForUi;
            ProgressBar.IsIndeterminate = false;
            ProgressDetail.Text = "";
            CloseButton.Visibility = Visibility.Visible;

            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                File.WriteAllText(
                    Path.Combine(baseDir, "launcher_last.log"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + messageForUser,
                    Encoding.UTF8
                );
            }
            catch
            {
                // ログの書き込みに失敗しても無視する
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}