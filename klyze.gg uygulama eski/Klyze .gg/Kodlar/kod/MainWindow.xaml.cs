using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using ValorantAutoClicker.Models;
using ValorantAutoClicker.Services;
using WinForms = System.Windows.Forms;

namespace ValorantAutoClicker
{
    public partial class MainWindow : Window
    {
        private ValorantAutoClicker.ViewModels.MainViewModel VM => App.MainVM;
        private IntPtr _hwnd = IntPtr.Zero;
        private WinForms.NotifyIcon _trayIcon;
        private DispatcherTimer _sidebarCollapseTimer;
        private bool _sidebarPinned = false;

        // Win32
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private static readonly uint[] FKeys = { 0x75, 0x76, 0x77, 0x78 }; // F6-F9
        private const int HK_AGENT = 9001;
        private const int HK_AFK = 9002;
        private const int HK_SPAM = 9003;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = VM;

            // Wire up events
            VM.PageChanged += OnPageChanged;
            VM.ThemeChanged += OnThemeChanged;
            VM.LanguageChanged += OnLanguageChanged;
            VM.StatusMessage += OnStatusMessage;

            InitTray();
            Loaded += (s, e) =>
            {
                var src = PresentationSource.FromVisual(this) as System.Windows.Interop.HwndSource;
                src?.AddHook(WndProc);
                _hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                RegisterHotKeys();
                VM.LoadConfig();

                // PlayerAnalysis DataContext
                if (PlayerAnalysisControl != null)
                {
                    var page = new ValorantAutoClicker.Views.PlayerAnalysisPage();
                    page.DataContext = VM.PlayerAnalysisVM;
                    PlayerAnalysisControl.Content = page;
                }

                // Play DataContext
                if (PlayPageControl != null)
                {
                    var playPage = new ValorantAutoClicker.Views.PlayPage();
                    playPage.DataContext = VM.PlayVM;
                    PlayPageControl.Content = playPage;
                }

                // Analiz DataContext
                if (AnalizPageControl != null)
                {
                    var analizPage = new ValorantAutoClicker.Views.AnalizPage();
                    analizPage.DataContext = VM.AnalizVM;
                    AnalizPageControl.Content = analizPage;
                }

                // Login DataContext
                if (LoginPageControl != null)
                {
                    var loginPage = new ValorantAutoClicker.Views.LoginPage();
                    loginPage.DataContext = VM.LoginVM;
                    LoginPageControl.Content = loginPage;
                }

                // Giriş yapıldı eventi
                VM.GirisYapildi += OnGirisYapildi;

                // Ayarlar sayfasındaki hesap bilgisini güncelle
                GuncelleHesapBilgisi();

                // İlk sayfa animasyonu
                if (HomePageStack != null)
                    PlayStaggeredEntry(HomePageStack);
            };

            Closing += (s, e) => { e.Cancel = true; HideToTray(); };
        }

        private void OnPageChanged(PageType page)
        {
            // Sayfa geçiş animasyonu
            var pages = new[] { HomePage, AgentPage, AfkPage, SpamPage, CrosshairPage, PlayerAnalysisPage, PlayPage, AnalizPage, SettingsPage, SupportPage, InfoPage };
            foreach (var p in pages)
                if (p != null) p.Visibility = Visibility.Collapsed;

            Grid newPage = page switch
            {
                PageType.Home => HomePage,
                PageType.Agent => AgentPage,
                PageType.Afk => AfkPage,
                PageType.Spam => SpamPage,
                PageType.Crosshair => CrosshairPage,
                PageType.PlayerAnalysis => PlayerAnalysisPage,
                PageType.Play => PlayPage, PageType.Analiz => AnalizPage, PageType.Settings => SettingsPage,
                PageType.Support => SupportPage,
                PageType.Info => InfoPage,
                _ => HomePage
            };

            if (newPage != null)
            {
                PlayPageTransition(newPage);
                // Analiz sayfasında DataContext ve verileri yenile
                if (page == PageType.Analiz && VM?.AnalizVM != null)
                {
                    // DataContext'i kontrol et veya ayarla
                    if (AnalizPageControl?.Content is Views.AnalizPage ap && ap.DataContext == null)
                    {
                        ap.DataContext = VM.AnalizVM;
                    }
                    try { _ = VM.AnalizVM.YukleAsync(); }
                    catch (Exception ex) { System.IO.File.AppendAllText("exe/error.log", $"[AnalizLoad] {ex}\n"); }
                }
                // Staggered entry for StackPanel children
                if (newPage.Children.Count > 0 && newPage.Children[0] is StackPanel sp)
                    PlayStaggeredEntry(sp);
            }

            // Nav buton aktif durumları
            SetNavActive(MenuHome, page == PageType.Home);
            SetNavActive(MenuAgent, page == PageType.Agent);
            SetNavActive(MenuAfk, page == PageType.Afk);
            SetNavActive(MenuSpam, page == PageType.Spam);
            SetNavActive(MenuCrosshair, page == PageType.Crosshair);
            SetNavActive(MenuPlayerAnalysis, page == PageType.PlayerAnalysis);
            SetNavActive(MenuPlay, page == PageType.Play); SetNavActive(MenuAnaliz, page == PageType.Analiz);
            SetNavActive(MenuSettings, page == PageType.Settings);
            SetNavActive(MenuSupport, page == PageType.Support);
            SetNavActive(MenuInfo, page == PageType.Info);

            // Sayfa seçilince sidebar 3 saniye sonra kapansın
            _sidebarCollapseTimer?.Stop();
            _sidebarCollapseTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _sidebarCollapseTimer.Tick += (s, args) =>
            {
                _sidebarCollapseTimer.Stop();
                CollapseSidebar();
            };
            _sidebarCollapseTimer.Start();
        }

        // Sayfa geçiş animasyonu: opacity 0 → 1, 200ms
        private void PlayPageTransition(Grid newPage)
        {
            newPage.Opacity = 0;
            newPage.Visibility = Visibility.Visible;
            var anim = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            newPage.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        // Staggered entry: elemanlar sırayla yukarıdan aşağıya kayarak girer
        private void PlayStaggeredEntry(Panel container)
        {
            if (container == null) return;
            var children = container.Children.Cast<UIElement>().ToList();
            int index = 0;
            foreach (var child in children)
            {
                var translateTransform = new TranslateTransform(0, -20);
                child.RenderTransform = translateTransform;
                child.Opacity = 0;

                var delay = TimeSpan.FromMilliseconds(index * 60);
                var duration = new Duration(TimeSpan.FromMilliseconds(300));
                var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

                var yAnim = new DoubleAnimation(-20, 0, duration)
                {
                    BeginTime = delay,
                    EasingFunction = ease
                };
                translateTransform.BeginAnimation(TranslateTransform.YProperty, yAnim);

                var opAnim = new DoubleAnimation(0, 1, duration)
                {
                    BeginTime = delay,
                    EasingFunction = ease
                };
                child.BeginAnimation(UIElement.OpacityProperty, opAnim);

                index++;
            }
        }

        private void SetNavActive(Button btn, bool active)
        {
            if (btn == null) return;
            btn.Background = active
                ? new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x25))
                : System.Windows.Media.Brushes.Transparent;
            btn.BorderBrush = active
                ? new SolidColorBrush(Colors.White)
                : System.Windows.Media.Brushes.Transparent;
            btn.BorderThickness = active ? new Thickness(2, 0, 0, 0) : new Thickness(0);
        }

        private void OnThemeChanged()
        {
            Resources["SidebarBg"] = VM.IsDarkMode
                ? new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A))
                : new SolidColorBrush(Colors.White);
        }

        private void OnLanguageChanged() { /* Refresh UI language */ }
        private void OnStatusMessage(string msg) { if (StatusText != null) StatusText.Text = msg; }

        private void RegisterHotKeys()
        {
            try
            {
                UnregisterHotKeys();
                int ai = 2;
                int fi = VM.SettingsVM?.HotkeyAfk ?? 0;
                int si = VM.SettingsVM?.HotkeySpam ?? 1;
                RegisterHotKey(_hwnd, HK_AGENT, 0, FKeys[Math.Max(0, Math.Min(3, ai))]);
                RegisterHotKey(_hwnd, HK_AFK, 0, FKeys[Math.Max(0, Math.Min(3, fi))]);
                RegisterHotKey(_hwnd, HK_SPAM, 0, FKeys[Math.Max(0, Math.Min(3, si))]);
            }
            catch (Exception ex)
            {
                LoggingService.Error("MainWindow", "Failed to register hotkeys", ex);
            }
        }

        private void UnregisterHotKeys()
        {
            try
            {
                UnregisterHotKey(_hwnd, HK_AGENT);
                UnregisterHotKey(_hwnd, HK_AFK);
                UnregisterHotKey(_hwnd, HK_SPAM);
            }
            catch { }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0312) // WM_HOTKEY
            {
                int id = wParam.ToInt32();
                Dispatcher.Invoke(() =>
                {
                    if (id == HK_AGENT) VM.AgentVM?.ToggleCommand?.Execute(null);
                    else if (id == HK_AFK) VM.AfkVM?.ToggleCommand?.Execute(null);
                    else if (id == HK_SPAM) VM.SpamVM?.ToggleCommand?.Execute(null);
                });
                handled = true;
            }
            return IntPtr.Zero;
        }

        // Tray icon
        private void InitTray()
        {
            try
            {
                _trayIcon = new WinForms.NotifyIcon
                {
                    Text = "Klyze v3.3.0",
                    Icon = System.IO.File.Exists("icon.ico")
                        ? new System.Drawing.Icon("icon.ico")
                        : System.Drawing.SystemIcons.Application,
                    Visible = false
                };

                var menu = new WinForms.ContextMenuStrip();
                var showItem = new WinForms.ToolStripMenuItem("Aç");
                showItem.Click += (s, e) => ShowFromTray();
                menu.Items.Add(showItem);
                menu.Items.Add(new WinForms.ToolStripSeparator());
                var exitItem = new WinForms.ToolStripMenuItem("Çıkış");
                exitItem.Click += (s, e) => ExitApp();
                menu.Items.Add(exitItem);
                _trayIcon.ContextMenuStrip = menu;
                _trayIcon.MouseClick += (s, e) =>
                {
                    if (e.Button == WinForms.MouseButtons.Left) ShowFromTray();
                };
            }
            catch (Exception ex)
            {
                LoggingService.Error("MainWindow", "Failed to init tray", ex);
            }
        }

        private void HideToTray()
        {
            if (_trayIcon != null) { _trayIcon.Visible = true; Hide(); }
            else WindowState = WindowState.Minimized;
        }

        private void ShowFromTray()
        {
            Show(); WindowState = WindowState.Normal; Activate();
            if (_trayIcon != null) _trayIcon.Visible = false;
        }

        private void ExitApp()
        {
            try { _trayIcon?.Dispose(); } catch { }
            UnregisterHotKeys();
            VM.SaveConfig();
            App.Current.Shutdown();
        }

        // Window chrome
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        { if (e.ChangedButton == MouseButton.Left) DragMove(); }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        { WindowState = WindowState.Minimized; }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        { HideToTray(); }

        private void ToggleSidebar_Click(object sender, RoutedEventArgs e)
        {
            // Artık kullanılmıyor — hover ile açılıp kapanıyor
        }

        // Araçlar accordion — tıkla aç/kapat
        private bool _toolsExpanded = false;

        private void ToolsMenu_Click(object sender, RoutedEventArgs e)
        {
            if (ToolsSubmenu == null) return;
            _toolsExpanded = !_toolsExpanded;
            double targetHeight = _toolsExpanded ? 220 : 0; // 5 item × 44px
            var anim = new DoubleAnimation(ToolsSubmenu.Height, targetHeight,
                new Duration(TimeSpan.FromMilliseconds(220)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ToolsSubmenu.BeginAnimation(FrameworkElement.HeightProperty, anim);
        }

        // Sidebar hover ile açılır
        private void Sidebar_MouseEnter(object sender, MouseEventArgs e)
        {
            _sidebarCollapseTimer?.Stop();
            ExpandSidebar();
        }

        // Fare çıkınca 3 saniye bekleyip kapanır
        private void Sidebar_MouseLeave(object sender, MouseEventArgs e)
        {
            _sidebarCollapseTimer?.Stop();
            _sidebarCollapseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _sidebarCollapseTimer.Tick += (s, args) =>
            {
                _sidebarCollapseTimer.Stop();
                CollapseSidebar();
            };
            _sidebarCollapseTimer.Start();
        }

        private void ExpandSidebar()
        {
            AnimateSidebarWidth(200);
            if (LblHome != null) LblHome.Visibility = Visibility.Visible;
            if (LblTools != null) LblTools.Visibility = Visibility.Visible;
            if (LblPlay != null) LblPlay.Visibility = Visibility.Visible;
            if (LblAnaliz != null) LblAnaliz.Visibility = Visibility.Visible;
            if (LblSettings != null) LblSettings.Visibility = Visibility.Visible;
            if (LblSupport != null) LblSupport.Visibility = Visibility.Visible;
            if (LblInfo != null) LblInfo.Visibility = Visibility.Visible;
        }

        private void CollapseSidebar()
        {
            AnimateSidebarWidth(60);
            if (LblHome != null) LblHome.Visibility = Visibility.Collapsed;
            if (LblTools != null) LblTools.Visibility = Visibility.Collapsed;
            if (LblPlay != null) LblPlay.Visibility = Visibility.Collapsed;
            if (LblAnaliz != null) LblAnaliz.Visibility = Visibility.Collapsed;
            if (LblSettings != null) LblSettings.Visibility = Visibility.Collapsed;
            if (LblSupport != null) LblSupport.Visibility = Visibility.Collapsed;
            if (LblInfo != null) LblInfo.Visibility = Visibility.Collapsed;
        }

        private void AnimateSidebarWidth(double targetWidth)
        {
            if (SidebarCol == null) return;
            var from = SidebarCol.Width.Value;
            var anim = new DoubleAnimation(from, targetWidth,
                new Duration(TimeSpan.FromMilliseconds(220)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            anim.Completed += (s, e) => SidebarCol.Width = new GridLength(targetWidth);
            // GridLength animasyonu için custom approach
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            double elapsed = 0;
            double duration = 220;
            timer.Tick += (s, e) =>
            {
                elapsed += 16;
                double t = Math.Min(elapsed / duration, 1.0);
                // CubicEase EaseOut: t = 1 - (1-t)^3
                double eased = 1 - Math.Pow(1 - t, 3);
                double current = from + (targetWidth - from) * eased;
                SidebarCol.Width = new GridLength(current);
                if (t >= 1.0)
                {
                    timer.Stop();
                    SidebarCol.Width = new GridLength(targetWidth);
                }
            };
            timer.Start();
        }

        // --- Event handlers that forward to ViewModel ---

        private void MenuHome_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(ValorantAutoClicker.Models.PageType.Home);
        private void MenuAgent_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(ValorantAutoClicker.Models.PageType.Agent);
        private void MenuAfk_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(ValorantAutoClicker.Models.PageType.Afk);
        private void MenuSpam_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(ValorantAutoClicker.Models.PageType.Spam);
        private void MenuCrosshair_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(ValorantAutoClicker.Models.PageType.Crosshair);
        private void MenuPlayerAnalysis_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(ValorantAutoClicker.Models.PageType.PlayerAnalysis);
        private void MenuPlay_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(ValorantAutoClicker.Models.PageType.Play);
        private void MenuAnaliz_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(ValorantAutoClicker.Models.PageType.Analiz);
        private void MenuSettings_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(ValorantAutoClicker.Models.PageType.Settings);
        private void MenuSupport_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(ValorantAutoClicker.Models.PageType.Support);
        private void MenuInfo_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(ValorantAutoClicker.Models.PageType.Info);

        // GameSelector kaldırıldı — oyun seçimi Ayarlar sayfasına taşınabilir
        private void GameSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // if (GameSelector?.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Content is System.Windows.Controls.StackPanel sp)
            //     foreach (var c in sp.Children)
            //         if (c is System.Windows.Controls.TextBlock tb) { VM.CurrentGame = tb.Text; break; }
        }

        private void AddPositionButton_Click(object sender, RoutedEventArgs e) => VM.AgentVM?.AddPositionCommand?.Execute(null);
        private void DeleteButton_Click(object sender, RoutedEventArgs e) => VM.AgentVM?.DeletePositionCommand?.Execute(PositionsCombo?.SelectedIndex);
        private void StartButton_Click(object sender, RoutedEventArgs e) => VM.AgentVM?.ToggleCommand?.Execute(null);

        private void SpeedSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (VM.AgentVM != null && SpeedValue != null)
                SpeedValue.Text = $"{(int)e.NewValue} ms";
        }

        private void AfkStartButton_Click(object sender, RoutedEventArgs e) => VM.AfkVM?.ToggleCommand?.Execute(null);
        private void AfkIntervalSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (AfkIntervalValue != null) AfkIntervalValue.Text = $"{(int)e.NewValue} saniye";
        }
        private void AfkInfinite_Changed(object sender, RoutedEventArgs e)
        {
            if (AfkDurationPanel != null && AfkInfiniteCheckbox != null)
                AfkDurationPanel.Visibility = AfkInfiniteCheckbox.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SpamStartButton_Click(object sender, RoutedEventArgs e) => VM.SpamVM?.ToggleCommand?.Execute(null);
        private void SpamSpeedSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (SpamSpeedValue != null) SpamSpeedValue.Text = $"{(int)e.NewValue} ms";
        }

        private void CrosshairProfileCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => VM.CrosshairVM?.RefreshProfileNames();
        private void SaveProfileButton_Click(object sender, RoutedEventArgs e) => VM.CrosshairVM?.SaveProfileCommand?.Execute(null);
        private void DeleteProfileButton_Click(object sender, RoutedEventArgs e) => VM.CrosshairVM?.DeleteProfileCommand?.Execute(null);
        private void ImportProfileButton_Click(object sender, RoutedEventArgs e) => VM.CrosshairVM?.ImportProfilesCommand?.Execute(null);
        private void ExportProfileButton_Click(object sender, RoutedEventArgs e) => VM.CrosshairVM?.ExportProfilesCommand?.Execute(null);
        private void EnableCrosshairButton_Click(object sender, RoutedEventArgs e) => VM.CrosshairVM?.ToggleCrosshairCommand?.Execute(null);

        private void CrosshairColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string color)
                VM.CrosshairVM?.SetColorCommand?.Execute(color);
        }

        private void CrosshairSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            // Update UI text values
            if (sender == InnerLengthSlider && InnerLengthValue != null) InnerLengthValue.Text = ((int)e.NewValue).ToString();
            else if (sender == InnerThicknessSlider && InnerThicknessValue != null) InnerThicknessValue.Text = ((int)e.NewValue).ToString();
            else if (sender == InnerGapSlider && InnerGapValue != null) InnerGapValue.Text = ((int)e.NewValue).ToString();
            else if (sender == OuterLengthSlider && OuterLengthValue != null) OuterLengthValue.Text = ((int)e.NewValue).ToString();
            else if (sender == OuterThicknessSlider && OuterThicknessValue != null) OuterThicknessValue.Text = ((int)e.NewValue).ToString();
            else if (sender == OuterGapSlider && OuterGapValue != null) OuterGapValue.Text = ((int)e.NewValue).ToString();
            else if (sender == CenterDotSizeSlider && CenterDotSizeValue != null) CenterDotSizeValue.Text = ((int)e.NewValue).ToString();
            else if (sender == ScaleSlider && ScaleValue != null) ScaleValue.Text = $"{e.NewValue:F1x}";
            else if (sender == OpacitySlider && OpacityValue != null) OpacityValue.Text = $"{(int)e.NewValue}%";
        }

        private void CrosshairCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            if (sender == OuterLinesCheckbox && OuterLinesPanel != null)
                OuterLinesPanel.Visibility = OuterLinesCheckbox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            else if (sender == CenterDotCheckbox && CenterDotPanel != null)
                CenterDotPanel.Visibility = CenterDotCheckbox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void HotkeyCombo_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { if (VM != null) VM.SaveConfig(); }

        private void LightTheme_Click(object sender, RoutedEventArgs e) { if (VM != null) { VM.IsDarkMode = false; VM.SaveConfig(); } }
        private void DarkTheme_Click(object sender, RoutedEventArgs e) { if (VM != null) { VM.IsDarkMode = true; VM.SaveConfig(); } }
        private void Turkish_Click(object sender, RoutedEventArgs e) { if (VM != null) { VM.SetLanguageCommand?.Execute("TR"); } }
        private void English_Click(object sender, RoutedEventArgs e) { if (VM != null) { VM.SetLanguageCommand?.Execute("EN"); } }

        private void Instagram_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => OpenUrl("https://www.instagram.com/autoclicker.g/");
        private void Youtube_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => OpenUrl("https://www.youtube.com/channel/UCp2HkORdCOwCqsthrbrFpRg/");
        private void Tiktok_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => OpenUrl("https://www.tiktok.com/@autoclicker.v");

        private void OpenUrl(string url)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true }); }
            catch { }
        }

        private void ProfileNameInput_TextChanged(object sender, TextChangedEventArgs e) { }

        // ─── Giriş / Çıkış ───────────────────────────────────────────────────────

        private void OnGirisYapildi()
        {
            Dispatcher.Invoke(() =>
            {
                GuncelleHesapBilgisi();
                // Giriş animasyonu: overlay kaybolur
                if (LoginOverlay != null)
                {
                    var anim = new System.Windows.Media.Animation.DoubleAnimation(1, 0,
                        new Duration(TimeSpan.FromMilliseconds(300)));
                    anim.Completed += (s, e) => LoginOverlay.Visibility = Visibility.Collapsed;
                    LoginOverlay.BeginAnimation(UIElement.OpacityProperty, anim);
                }
            });
        }

        private void CikisYap_Click(object sender, RoutedEventArgs e)
        {
            VM.CikisYap();
            GuncelleHesapBilgisi();
            // Giriş ekranını göster (fade in)
            if (LoginOverlay != null)
            {
                LoginOverlay.Opacity = 0;
                LoginOverlay.Visibility = Visibility.Visible;
                var anim = new System.Windows.Media.Animation.DoubleAnimation(0, 1,
                    new Duration(TimeSpan.FromMilliseconds(300)));
                LoginOverlay.BeginAnimation(UIElement.OpacityProperty, anim);
            }
        }

        private void GuncelleHesapBilgisi()
        {
            var profil = VM.UserService?.GetProfile();
            if (HesapAdiText != null)
                HesapAdiText.Text = profil?.GecerliMi == true ? profil.RiotId : "Giriş yapılmadı";
            if (HesapRutbeText != null)
                HesapRutbeText.Text = profil?.GecerliMi == true
                    ? $"{profil.Rutbe}  •  {profil.RutbePuani} RR  •  %{profil.KazanmaOrani:F0} kazanma"
                    : "";
            if (HesapDurumText != null)
            {
                HesapDurumText.Text = profil?.GecerliMi == true ? "Bağlı" : "Bağlı Değil";
                HesapDurumText.Foreground = profil?.GecerliMi == true
                    ? new SolidColorBrush(Color.FromRgb(0x00, 0xD2, 0x6A))
                    : new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
            }
        }
    }
}
