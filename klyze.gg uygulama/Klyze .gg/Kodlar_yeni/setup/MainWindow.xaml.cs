using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;

namespace KlyzeSetup
{
    public partial class MainWindow : Window
    {
        private int _currentPage = 0;
        private readonly int _totalPages = 5;
        private string _installPath = "";
        private readonly string[] _pages;

        public MainWindow()
        {
            InitializeComponent();

            _installPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Klyze");

            InstallPathText.Text = _installPath;
            _pages = new[] { "PageWelcome", "PageLicense", "PagePath", "PageProgress", "PageComplete" };
            UpdateUI();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage == 4)
                Close();
            else
            {
                var result = System.Windows.MessageBox.Show("Kurulum iptal edilsin mi?", "Klyze Setup",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                    Close();
            }
        }

        private void NextBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage == 0) { ShowPage(1); }
            else if (_currentPage == 1)
            {
                if (LicenseCheck.IsChecked != true)
                {
                    System.Windows.MessageBox.Show("Lütfen kullanım koşullarını kabul edin.", "Uyarı",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                ShowPage(2);
            }
            else if (_currentPage == 2)
            {
                if (!Directory.Exists(_installPath))
                {
                    try { Directory.CreateDirectory(_installPath); }
                    catch
                    {
                        System.Windows.MessageBox.Show("Bu konuma yazma izniniz yok. Lütfen farklı bir konum seçin.", "Hata",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                ShowPage(3);
                _ = InstallAsync();
            }
            else if (_currentPage == 4)
            {
                if (LaunchCheck.IsChecked == true)
                {
                    var exePath = Path.Combine(_installPath, "Klyze.exe");
                    if (File.Exists(exePath))
                    {
                        try { Process.Start(exePath); } catch { }
                    }
                }
                Close();
            }
        }

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 0 && _currentPage < 4)
                ShowPage(_currentPage - 1);
        }

        private void BrowsePath_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog();
            dialog.SelectedPath = _installPath;
            dialog.Description = "Klyze kurulum klasörünü seçin";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _installPath = dialog.SelectedPath;
                InstallPathText.Text = _installPath;
            }
        }

        private void LicenseCheck_Changed(object sender, RoutedEventArgs e)
        {
            UpdateUI();
        }

        private void ShowPage(int index)
        {
            _currentPage = index;
            for (int i = 0; i < _pages.Length; i++)
            {
                var grid = FindName(_pages[i]) as System.Windows.Controls.Grid;
                if (grid != null)
                    grid.Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
            }
            UpdateUI();
        }

        private void UpdateUI()
        {
            StepText.Text = $"{_currentPage + 1}/{_totalPages}";

            if (_currentPage == 0)
            {
                BackBtn.Visibility = Visibility.Collapsed;
                NextBtn.Content = "İleri →";
                NextBtn.Visibility = Visibility.Visible;
            }
            else if (_currentPage < 3)
            {
                BackBtn.Visibility = Visibility.Visible;
                NextBtn.Content = "İleri →";
                NextBtn.Visibility = Visibility.Visible;
                NextBtn.IsEnabled = _currentPage != 1 || LicenseCheck.IsChecked == true;
            }
            else if (_currentPage == 3)
            {
                BackBtn.Visibility = Visibility.Collapsed;
                NextBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                BackBtn.Visibility = Visibility.Collapsed;
                NextBtn.Visibility = Visibility.Visible;
                NextBtn.Content = "Bitir";
            }
        }

        private async Task InstallAsync()
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    ProgressTitle.Text = "Kuruluyor...";
                    ProgressText.Text = "Kurulum paketi açılıyor...";
                });

                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "KlyzeSetup.klyze_update.zip";

                await Task.Delay(300);

                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                        throw new Exception("Kurulum paketi bulunamadı.");

                    using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                    {
                        var entries = archive.Entries;
                        int total = entries.Count;
                        int completed = 0;

                        foreach (var entry in entries)
                        {
                            var destPath = Path.Combine(_installPath, entry.FullName);
                            var destDir = Path.GetDirectoryName(destPath);
                            if (!string.IsNullOrEmpty(destDir))
                                Directory.CreateDirectory(destDir);

                            if (!string.IsNullOrEmpty(entry.Name))
                                entry.ExtractToFile(destPath, overwrite: true);

                            completed++;
                            int progress = (int)((double)completed / total * 100);

                            await Dispatcher.InvokeAsync(() =>
                            {
                                ProgressBar.Width = progress * 4;
                                ProgressText.Text = $"Dosyalar çıkarılıyor... ({completed}/{total})";
                                ProgressDetail.Text = entry.FullName;
                            });

                            await Task.Delay(20);
                        }
                    }
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    ProgressTitle.Text = "Kısayollar oluşturuluyor...";
                    ProgressText.Text = "Masaüstü kısayolu ekleniyor...";
                });

                await Task.Delay(200);

                var targetExe = Path.Combine(_installPath, "Klyze.exe");
                CreateShortcut(targetExe);

                await Dispatcher.InvokeAsync(() =>
                {
                    ProgressBar.Width = 400;
                    FinalPathText.Text = _installPath;
                    ShowPage(4);
                    NextBtn.Visibility = Visibility.Visible;
                    NextBtn.Content = "Bitir";
                    BackBtn.Visibility = Visibility.Collapsed;
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    System.Windows.MessageBox.Show($"Kurulum sırasında hata oluştu:\n{ex.Message}", "Hata",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    ShowPage(2);
                    NextBtn.Visibility = Visibility.Visible;
                    BackBtn.Visibility = Visibility.Visible;
                });
            }
        }

        private void CreateShortcut(string targetExe)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var shortcutPath = Path.Combine(desktop, "Klyze.lnk");

            try
            {
                var psi = new ProcessStartInfo("powershell")
                {
                    Arguments = $"-Command \"$ws=New-Object -ComObject WScript.Shell;$s=$ws.CreateShortcut('{shortcutPath}');$s.TargetPath='{targetExe}';$s.WorkingDirectory='{Path.GetDirectoryName(targetExe)}';$s.Description='Klyze - Valorant Araç Seti';$s.Save()\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                var proc = Process.Start(psi);
                proc?.WaitForExit(5000);
            }
            catch { }

            var startMenu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                "Programs", "Klyze.lnk");

            if (!File.Exists(startMenu))
            {
                try
                {
                    File.Copy(shortcutPath, startMenu, overwrite: true);
                }
                catch { }
            }
        }
    }
}
