using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using ValorantAutoClicker.Helpers;
using ValorantAutoClicker.Models;
using ValorantAutoClicker.Services;
using ValorantAutoClicker.ViewModels;
using WinForms = System.Windows.Forms;

namespace ValorantAutoClicker
{
    public partial class MainWindow : Window
    {
        private ValorantAutoClicker.ViewModels.MainViewModel VM => App.MainVM;
        private IntPtr _hwnd = IntPtr.Zero;
        private WinForms.NotifyIcon _trayIcon;
        private DispatcherTimer _sidebarCollapseTimer;
        private ObservableCollection<SearchItem> _searchItems;
        private List<SearchItem> _allSearchItems;
        private Views.CrosshairOverlayWindow _crosshairOverlay;

        private class SearchItem
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public string Icon { get; set; }
            public PageType Page { get; set; }
        }

        // Win32
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        private static readonly uint[] FKeys = { 0x75, 0x76, 0x77, 0x78 }; // F6-F9

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEWHEEL = 0x020A;
        private IntPtr _mouseHook;
        private LowLevelMouseProc _mouseProc;

        private const int HK_AGENT = 9001;
        private const int HK_AFK = 9002;
        private const int HK_SPAM = 9003;
        private const int HK_FAKEMIC = 9004;
        private const int HK_FAKEMIC_FILE_BASE = 9010;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = VM;

            EventManager.RegisterClassHandler(
                typeof(UIElement),
                UIElement.PreviewMouseWheelEvent,
                new MouseWheelEventHandler(OnAnyPreviewMouseWheel),
                true);

            // Wire up events
            VM.PageChanged += OnPageChanged;
            VM.ThemeChanged += OnThemeChanged;
            VM.LanguageChanged += OnLanguageChanged;
            VM.StatusMessage += OnStatusMessage;

            // Pulse animasyonu + güncelleme UI
            VM.PropertyChanged += (s, args) =>
            {
                switch (args.PropertyName)
                {
                    case nameof(VM.UpdateAvailable):
                        Dispatcher.Invoke(() =>
                        {
                            if (VM.UpdateAvailable)
                                BaslatPulseAnimasyonu();
                            else
                                DurdurPulseAnimasyonu();
                        });
                        break;
                    case nameof(VM.DownloadProgress):
                        Dispatcher.Invoke(() =>
                        {
                            var p = VM.DownloadProgress;
                            if (PanelProgressTrack.ActualWidth <= 0)
                                PanelProgressTrack.UpdateLayout();
                            PanelProgressFill.Width = PanelProgressTrack.ActualWidth > 0
                                ? (p * PanelProgressTrack.ActualWidth) / 100 : 0;
                            PanelGuncelleBtnText.Text = $"İndiriliyor... %{p}";
                            if (p >= 100) PanelGuncelleBtnText.Text = "Kuruluyor...";
                        });
                        break;
                    case nameof(VM.DownloadStatus):
                        Dispatcher.Invoke(() => GuncellemeDurumGuncelle(VM.DownloadStatus));
                        break;
                    case nameof(VM.IsDownloading):
                        if (!VM.IsDownloading && !VM.UpdateAvailable)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                PanelProgressTrack.Visibility = Visibility.Collapsed;
                                PanelGuncelleBtnText.Text = "Güncelle";
                            });
                        }
                        break;
                }
            };

            InitTray();
            Loaded += (s, e) =>
            {
                var src = PresentationSource.FromVisual(this) as System.Windows.Interop.HwndSource;
                src?.AddHook(WndProc);
                _hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                RegisterHotKeys();

                // Install low-level mouse hook (required for AllowsTransparency windows)
                _mouseProc = MouseHookCallback;
                IntPtr hMod = System.Runtime.InteropServices.Marshal.GetHINSTANCE(
                    typeof(MainWindow).Module);
                _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, hMod, 0);
                if (_mouseHook == IntPtr.Zero)
                {
                    int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                }

                VM.LoadConfig();

                // Analiz DataContext
                if (AnalizPageControl != null)
                {
                    var analizPage = new ValorantAutoClicker.Views.AnalizPage();
                    analizPage.DataContext = VM.AnalizVM;
                    AnalizPageControl.Content = analizPage;
                }

                // Play DataContext
                if (PlayPageControl != null)
                {
                    var playPage = new ValorantAutoClicker.Views.PlayPage();
                    playPage.DataContext = VM.PlayVM;
                    PlayPageControl.Content = playPage;
                }

                // Login DataContext
                if (LoginPageControl != null)
                {
                    var loginPage = new ValorantAutoClicker.Views.LoginPage();
                    loginPage.DataContext = VM.LoginVM;
                    LoginPageControl.Content = loginPage;
                }

                // Klyze AI events

                System.ComponentModel.PropertyChangedEventHandler aiSohbetHandler = null;
                System.Collections.Specialized.NotifyCollectionChangedEventHandler aiMesajHandler = null;

                aiSohbetHandler = (s, pce) =>
                {
                    if (pce.PropertyName == nameof(VM.KlyzeAiVM.AktifSohbet) && Dispatcher != null)
                    {
                        Dispatcher.Invoke(async () =>
                        {
                            if (VM.KlyzeAiVM.AktifSohbet?.Mesajlar != null)
                            {
                                VM.KlyzeAiVM.AktifSohbet.Mesajlar.CollectionChanged += aiMesajHandler;
                                await Task.Delay(50);
                                //if (AiChatScroll != null)
                                //    AiChatScroll.ScrollToVerticalOffset(AiChatScroll.ScrollableHeight);
                            }
                        });
                    }
                };
                VM.KlyzeAiVM.PropertyChanged += aiSohbetHandler;

                aiMesajHandler = (s, cce) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (AiChatScroll != null)
                            AiChatScroll.ScrollToVerticalOffset(AiChatScroll.ScrollableHeight);
                    });
                };

                if (VM.KlyzeAiVM.AktifSohbet?.Mesajlar != null)
                    VM.KlyzeAiVM.AktifSohbet.Mesajlar.CollectionChanged += aiMesajHandler;

                // Giriş yapıldı eventi
                VM.GirisYapildi += OnGirisYapildi;

                // Zil pulse animasyonu (eğer UpdateAvailable zaten true ise)
                if (VM.UpdateAvailable)
                    BaslatPulseAnimasyonu();

                // Ayarlar sayfasındaki hesap bilgisini güncelle
                GuncelleHesapBilgisi();

                SetNavActive(MenuHome, true);

                InitSearch();
                LoadEquipmentData();
                LoadSavedProfiles();
                SearchBox.TextChanged += SearchBox_TextChanged;
                SearchBox.KeyDown += SearchBox_KeyDown;
                SearchClearBtn.MouseLeftButtonUp += (s, e) => { SearchBox.Text = ""; SearchPopup.IsOpen = false; SearchClearBtn.Visibility = Visibility.Collapsed; };
                SearchResultsList.PreviewMouseLeftButtonUp += (s, e) => { if (SearchResultsList.SelectedItem != null) ExecuteSearchSelection(); };
            };

            Closing += (s, e) => { e.Cancel = true; HideToTray(); };
            Closed += (s, e) =>
            {
                if (_mouseHook != IntPtr.Zero)
                    UnhookWindowsHookEx(_mouseHook);
            };
        }

        private void LoadEquipmentData()
        {
            try
            {
                var path = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Klyze", "ekipman.json");
                if (System.IO.File.Exists(path))
                {
                    var json = System.IO.File.ReadAllText(path);
                    var data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (data != null)
                    {
                        EquipMouse.Text = data.GetValueOrDefault("mouse", "");
                        EquipKeyboard.Text = data.GetValueOrDefault("keyboard", "");
                        EquipMonitor.Text = data.GetValueOrDefault("monitor", "");
                        EquipHeadset.Text = data.GetValueOrDefault("headset", "");
                        EquipMousepad.Text = data.GetValueOrDefault("mousepad", "");
                        ShowEquipSavedView(data);
                    }
                }
            }
            catch { }
        }

        private void InitSearch()
        {
            _allSearchItems = new List<SearchItem>
            {
                new SearchItem { Name = "Ana Sayfa", Description = "Ana sayfaya dön", Icon = "🏠", Page = PageType.Home },

                new SearchItem { Name = "Crosshair", Description = "Crosshair ayarları ve yönetimi", Icon = "🎯", Page = PageType.Crosshair },
                new SearchItem { Name = "Fake Ses", Description = "Sanal mikrofon ile ses oynatma", Icon = "🎤", Page = PageType.FakeMic },
                new SearchItem { Name = "Oyna", Description = "Maç bulma ve lobi yönetimi", Icon = "🎮", Page = PageType.Play },
                new SearchItem { Name = "Analiz", Description = "Maç istatistikleri ve performans analizi", Icon = "📊", Page = PageType.Analiz },
                new SearchItem { Name = "Ayarlar", Description = "Uygulama ayarları ve kısayol tuşları", Icon = "⚙️", Page = PageType.Settings },
                new SearchItem { Name = "Destek", Description = "Yardım ve destek sayfası", Icon = "❓", Page = PageType.Support },
                new SearchItem { Name = "Bilgi", Description = "Uygulama hakkında bilgi", Icon = "ℹ️", Page = PageType.Info },
                new SearchItem { Name = "Timer", Description = "Zamanlayıcı ve geri sayım", Icon = "⏱️", Page = PageType.Timer },

            };
            _searchItems = new ObservableCollection<SearchItem>();
            SearchResultsList.ItemsSource = _searchItems;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = SearchBox.Text?.Trim().ToLower();
            SearchClearBtn.Visibility = string.IsNullOrEmpty(query) ? Visibility.Collapsed : Visibility.Visible;

            if (string.IsNullOrEmpty(query))
            {
                SearchPopup.IsOpen = false;
                return;
            }

            var results = _allSearchItems
                .Where(x => x.Name.ToLower().Contains(query) || x.Description.ToLower().Contains(query))
                .ToList();

            _searchItems.Clear();
            foreach (var item in results)
                _searchItems.Add(item);

            SearchPopup.IsOpen = _searchItems.Count > 0;
            if (_searchItems.Count > 0)
                SearchResultsList.SelectedIndex = 0;
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down)
            {
                if (SearchResultsList.SelectedIndex < _searchItems.Count - 1)
                    SearchResultsList.SelectedIndex++;
                e.Handled = true;
            }
            else if (e.Key == Key.Up)
            {
                if (SearchResultsList.SelectedIndex > 0)
                    SearchResultsList.SelectedIndex--;
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                ExecuteSearchSelection();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                SearchPopup.IsOpen = false;
                SearchBox.Text = "";
                Keyboard.ClearFocus();
                e.Handled = true;
            }
        }

        private void SearchResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SearchResultsList.SelectedItem is SearchItem item)
            {
                if (Keyboard.IsKeyDown(Key.Enter))
                    ExecuteSearchSelection();
            }
        }

        private void ExecuteSearchSelection()
        {
            if (SearchResultsList.SelectedItem is SearchItem item)
            {
                SearchPopup.IsOpen = false;
                SearchBox.Text = "";
                SearchClearBtn.Visibility = Visibility.Collapsed;
                Keyboard.ClearFocus();
                VM?.NavigateCommand.Execute(item.Page);
            }
        }

        private void OnPageChanged(PageType page)
        {
            var pages = new[] { HomePage, AgentPage, AfkPage, SpamPage, CrosshairPage, FakeMicPage, PlayPage, AnalizPage, SettingsPage, SupportPage, InfoPage, TimerPage, KlyzeAiPage };
            foreach (var p in pages)
                if (p != null) p.Visibility = Visibility.Collapsed;

            Grid newPage = page switch
            {
                PageType.Home => HomePage,
                PageType.Agent => AgentPage,
                PageType.Afk => AfkPage,
                PageType.Spam => SpamPage,
                PageType.Crosshair => CrosshairPage,
                PageType.FakeMic => FakeMicPage,
                PageType.Play => PlayPage,
                PageType.Analiz => AnalizPage,
                PageType.Settings => SettingsPage,
                PageType.Support => SupportPage,
                PageType.Info => InfoPage,
                PageType.Timer => TimerPage,
                PageType.KlyzeAi => KlyzeAiPage,
                _ => HomePage
            };

            if (newPage != null)
            {
                PlayPageTransition(newPage);
                if (page == PageType.Analiz && VM?.AnalizVM != null)
                {
                    if (AnalizPageControl?.Content is Views.AnalizPage ap && ap.DataContext == null)
                        ap.DataContext = VM.AnalizVM;
                    try { _ = VM.AnalizVM.YukleAsync(); }
                    catch { }
                }
                if (page == PageType.Agent) LoadSavedProfiles();
                if (page == PageType.Crosshair) InitCrosshairPage();
                if (page == PageType.FakeMic) InitFakeMicPage();
                if (page == PageType.KlyzeAi)
                {
                    ExpandToolsIfNeeded();
                    Dispatcher.InvokeAsync(() =>
                    {
                        if (AiChatScroll != null)
                            AiChatScroll.ScrollToVerticalOffset(AiChatScroll.ScrollableHeight);
                    });
                }
                if (newPage.Children.Count > 0 && newPage.Children[0] is StackPanel sp)
                    PlayStaggeredEntry(sp);
            }

            // Nav buton aktif durumları
            SetNavActive(MenuHome, page == PageType.Home);
            SetNavActive(MenuCrosshair, page == PageType.Crosshair);
            SetNavActive(MenuFakeMic, page == PageType.FakeMic);
            SetNavActive(MenuPlay, page == PageType.Play);
            SetNavActive(MenuAnaliz, page == PageType.Analiz);
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

                var delay = TimeSpan.FromMilliseconds(index * 80);
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
        private void OnStatusMessage(string msg)
        {
            if (StatusText != null) StatusText.Text = msg;
            if (msg != null && msg.Contains("eklendi"))
            {
                UpdatePositionList();
            }
        }

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
                int fmVk = VM.SettingsVM?.HotkeyFakeMic ?? 0x78;
                RegisterHotKey(_hwnd, HK_FAKEMIC, 0, (uint)fmVk);

                // Per-file hotkeys
                var fvm = VM?.FakeMicVM;
                if (fvm != null)
                {
                    for (int i = 0; i < fvm.Playlist.Count; i++)
                    {
                        int vk = fvm.Playlist[i].HotkeyVk;
                        if (vk > 0)
                            RegisterHotKey(_hwnd, HK_FAKEMIC_FILE_BASE + i, 0, (uint)vk);
                    }
                }
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
                UnregisterHotKey(_hwnd, HK_FAKEMIC);
                for (int i = 0; i < 20; i++)
                    UnregisterHotKey(_hwnd, HK_FAKEMIC_FILE_BASE + i);
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
                    else if (id == HK_FAKEMIC) HandleFakeMicHotkey();
                    else if (id >= HK_FAKEMIC_FILE_BASE)
                    {
                        int idx = id - HK_FAKEMIC_FILE_BASE;
                        var fvm = VM?.FakeMicVM;
                        if (fvm != null && idx >= 0 && idx < fvm.Playlist.Count)
                            fvm.PlayFile(fvm.Playlist[idx]);
                    }
                });
                handled = true;
            }
            else if (msg == 0x020A) // WM_MOUSEWHEEL
            {
                try
                {
                    int delta = (short)HIWORD(wParam);
                    bool h = false;
                    if (AnalizPage.Visibility == Visibility.Visible &&
                        AnalizPageControl?.Content is Views.AnalizPage ap &&
                        ap.MacDetayBackdrop.Visibility == Visibility.Visible)
                    {
                        ap.MacDetayScroll.ScrollToVerticalOffset(
                            ap.MacDetayScroll.VerticalOffset - delta);
                        h = true;
                    }
                    handled = h;
                }
                catch { }
            }
            return IntPtr.Zero;
        }

        private static int HIWORD(IntPtr ptr) => (int)((long)ptr >> 16) & 0xFFFF;

        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_MOUSEWHEEL)
            {
                int delta = (short)(System.Runtime.InteropServices.Marshal.ReadInt32(lParam, 8) >> 16);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (AnalizPage.Visibility == Visibility.Visible &&
                            AnalizPageControl?.Content is Views.AnalizPage ap &&
                            ap.MacDetayBackdrop.Visibility == Visibility.Visible)
                        {
                            ap.MacDetayScroll.ScrollToVerticalOffset(
                                ap.MacDetayScroll.VerticalOffset - delta);
                        }
                    }
                    catch { }
                }));
            }
            return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        // Tray icon
        private void InitTray()
        {
            try
            {
                var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                _trayIcon = new WinForms.NotifyIcon
                {
                    Text = "Klyze v3.12.0",
                    Icon = System.IO.File.Exists(iconPath)
                        ? new System.Drawing.Icon(iconPath)
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

        private void OnAnyPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (AnalizPage.Visibility == Visibility.Visible &&
                AnalizPageControl?.Content is Views.AnalizPage ap &&
                ap.MacDetayBackdrop.Visibility == Visibility.Visible)
            {
                var sv = ap.MacDetayScroll;
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
                e.Handled = true;
            }
        }

        // --- Sidebar nav click handlers ---

        private void MenuHome_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(PageType.Home);
        private void MenuAgent_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(PageType.Agent);
        private void MenuAfk_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(PageType.Afk);
        private void MenuSpam_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(PageType.Spam);
        private void MenuCrosshair_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(PageType.Crosshair);
        private void MenuFakeMic_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(PageType.FakeMic);
        private void MenuPlay_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(PageType.Play);
        private void MenuAnaliz_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(PageType.Analiz);
        private void MenuSettings_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(PageType.Settings);
        private void MenuSupport_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(PageType.Support);
        private void MenuInfo_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(PageType.Info);
        private void MenuKlyzeAi_Click(object sender, RoutedEventArgs e)
            => VM.NavigateCommand.Execute(PageType.KlyzeAi);
        private void KlyzeAiGirisKutusu_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && VM?.KlyzeAiVM?.GonderCommand?.CanExecute(null) == true)
                VM.KlyzeAiVM.GonderCommand.Execute(null);
        }

        // Araçlar accordion — tıkla aç/kapat
        private bool _toolsExpanded = false;

        private void ToolsMenu_Click(object sender, RoutedEventArgs e)
        {
            if (ToolsSubmenu == null) return;
            _toolsExpanded = !_toolsExpanded;
            double targetHeight = _toolsExpanded ? 88 : 0;
            var anim = new DoubleAnimation(ToolsSubmenu.Height, targetHeight,
                new Duration(TimeSpan.FromMilliseconds(220)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ToolsSubmenu.BeginAnimation(FrameworkElement.HeightProperty, anim);
        }

        private void ExpandToolsIfNeeded()
        {
            if (ToolsSubmenu == null || _toolsExpanded) return;
            _toolsExpanded = true;
            double targetHeight = 88;
            var anim = new DoubleAnimation(ToolsSubmenu.Height, targetHeight,
                new Duration(TimeSpan.FromMilliseconds(220)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ToolsSubmenu.BeginAnimation(FrameworkElement.HeightProperty, anim);
        }

        private void Sidebar_MouseEnter(object sender, MouseEventArgs e)
        {
            _sidebarCollapseTimer?.Stop();
            ExpandSidebar();
        }

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

            // Tools alt menüsü açıksa kapat
            if (_toolsExpanded)
            {
                _toolsExpanded = false;
                if (ToolsSubmenu != null)
                {
                    var anim = new DoubleAnimation(ToolsSubmenu.Height, 0,
                        new Duration(TimeSpan.FromMilliseconds(150)))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
                    ToolsSubmenu.BeginAnimation(FrameworkElement.HeightProperty, anim);
                }
            }
        }

        private void AnimateSidebarWidth(double targetWidth)
        {
            if (SidebarCol == null) return;
            var from = SidebarCol.Width.Value;
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

        // GameSelector kaldırıldı — oyun seçimi Ayarlar sayfasına taşınabilir
        private void GameSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // if (GameSelector?.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Content is System.Windows.Controls.StackPanel sp)
            //     foreach (var c in sp.Children)
            //         if (c is System.Windows.Controls.TextBlock tb) { VM.CurrentGame = tb.Text; break; }
        }
        private void PositionDelete_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is int idx)
            {
                VM.AgentVM?.DeletePositionCommand?.Execute(idx - 1);
                UpdatePositionList();
                if (VM?.AgentVM?.Positions?.Count == 0)
                {
                    AddPosBtn.IsEnabled = true;
                }
            }
        }

        private void AddPositionButton_Click(object sender, RoutedEventArgs e)
        {
            VM.AgentVM?.AddPositionCommand?.Execute(null);
            StatusText.Text = "⏳ 3 saniye içinde tıklanacak yere git...";
            UpdatePositionList();
            if (VM?.AgentVM?.Positions?.Count > 0) AddPosBtn.IsEnabled = false;
        }

        private void NewProfile_Click(object sender, RoutedEventArgs e)
        {
            AgentProfileName.Text = "";
            AgentEditor.Visibility = Visibility.Visible;
            UpdatePositionList();
            StatusText.Text = "Profil adı gir, pozisyon ekle ve kaydet.";
        }

        private void AgentSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            var profileName = AgentProfileName.Text?.Trim();
            if (string.IsNullOrEmpty(profileName))
            {
                StatusText.Text = "✖ Lütfen profil adı girin.";
                return;
            }
            if (VM?.AgentVM?.Positions == null || VM.AgentVM.Positions.Count == 0)
            {
                StatusText.Text = "✖ En az 1 pozisyon ekleyin.";
                return;
            }

            try
            {
                var profiles = LoadAgentProfiles();
                var posList = VM.AgentVM.Positions.Select(p => new { X = (int)p.X, Y = (int)p.Y }).ToList();
                var profile = new { Name = profileName, Positions = posList };
                profiles.Add(profile);
                SaveAgentProfiles(profiles);
                AgentEditor.Visibility = Visibility.Collapsed;
                StatusText.Text = $"✔ '{profileName}' profili kaydedildi!";
                LoadSavedProfiles();
            }
            catch (Exception ex)
            {
                StatusText.Text = "✖ Hata: " + ex.Message;
            }
        }

        private System.Collections.Generic.List<object> LoadAgentProfiles()
        {
            var path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Klyze", "ajan_profilleri.json");
            if (System.IO.File.Exists(path))
            {
                var json = System.IO.File.ReadAllText(path);
                return System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<object>>(json) ?? new System.Collections.Generic.List<object>();
            }
            return new System.Collections.Generic.List<object>();
        }

        private void SaveAgentProfiles(System.Collections.Generic.List<object> profiles)
        {
            var path = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Klyze", "ajan_profilleri.json");
            var json = System.Text.Json.JsonSerializer.Serialize(profiles, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(path, json);
        }

        private void LoadSavedProfiles()
        {
            try
            {
                var profiles = LoadAgentProfiles();
                SavedProfilesPanel.Children.Clear();
                if (profiles.Count == 0)
                {
                    SavedProfilesPanel.Children.Add(new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(14),
                        Child = new TextBlock
                        {
                            Text = "Henüz profil kaydedilmedi. Yeni profil oluştur.",
                            FontSize = 11,
                            Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    });
                    return;
                }
                int profileIndex = 0;
                foreach (var p in profiles)
                {
                    var json = System.Text.Json.JsonSerializer.Serialize(p);
                    var dict = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(json);
                    var name = dict?.GetValueOrDefault("Name")?.ToString() ?? "İsimsiz";
                    int posCount = 0;
                    if (dict?.ContainsKey("Positions") == true && dict["Positions"] is System.Text.Json.JsonElement arr)
                        posCount = arr.GetArrayLength();

                    var border = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                        CornerRadius = new CornerRadius(8),
                        Padding = new Thickness(14),
                        Margin = new Thickness(0, 0, 0, 6),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                        BorderThickness = new Thickness(1)
                    };
                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var numBorder = new Border
                    {
                        Width = 24, Height = 24,
                        Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)),
                        CornerRadius = new CornerRadius(6),
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    Grid.SetColumn(numBorder, 0);
                    numBorder.Child = new TextBlock
                    {
                        Text = "P",
                        FontSize = 11, FontWeight = FontWeights.Bold,
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    grid.Children.Add(numBorder);

                    var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(stack, 1);
                    stack.Children.Add(new TextBlock
                    {
                        Text = name,
                        FontSize = 11, FontWeight = FontWeights.SemiBold,
                        Foreground = Brushes.White
                    });
                    stack.Children.Add(new TextBlock
                    {
                        Text = $"{posCount} pozisyon",
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88))
                    });
                    grid.Children.Add(stack);

                    var delBtn = new Border
                    {
                        Width = 28, Height = 28,
                        Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                        CornerRadius = new CornerRadius(6),
                        Cursor = Cursors.Hand,
                        Tag = profileIndex
                    };
                    Grid.SetColumn(delBtn, 2);
                    delBtn.MouseLeftButtonUp += ProfileDelete_Click;
                    delBtn.Child = new TextBlock
                    {
                        Text = "✕",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x46, 0x55)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    grid.Children.Add(delBtn);

                    border.Child = grid;
                    SavedProfilesPanel.Children.Add(border);
                    profileIndex++;
                }

                // Populate profile combo
                AgentProfileCombo.Items.Clear();
                AgentProfileCombo.Items.Add(new ComboBoxItem { Content = "-- Profil Seç --", IsSelected = true, Tag = "" });
                foreach (var p in profiles)
                {
                    var j = System.Text.Json.JsonSerializer.Serialize(p);
                    var d = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(j);
                    var n = d?.GetValueOrDefault("Name")?.ToString() ?? "İsimsiz";
                    AgentProfileCombo.Items.Add(new ComboBoxItem { Content = n, Tag = j });
                }
            }
            catch { }
        }

        private void ProfileDelete_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Tag is int index)
            {
                try
                {
                    var profiles = LoadAgentProfiles();
                    if (index >= 0 && index < profiles.Count)
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(profiles[index]);
                        var dict = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(json);
                        var name = dict?.GetValueOrDefault("Name")?.ToString() ?? "İsimsiz";
                        profiles.RemoveAt(index);
                        SaveAgentProfiles(profiles);
                        LoadSavedProfiles();
                        StatusText.Text = $"✔ '{name}' silindi!";
                    }
                }
                catch (Exception ex)
                {
                    StatusText.Text = "✖ Silme hatası: " + ex.Message;
                }
            }
        }

        private void UpdatePositionList()
        {
            if (VM?.AgentVM?.Positions == null) return;
            var items = new System.Collections.ObjectModel.ObservableCollection<object>();
            int idx = 1;
            foreach (var p in VM.AgentVM.Positions)
            {
                var item = new { Index = idx, Coord = $"X: {(int)p.X}, Y: {(int)p.Y}" };
                items.Add(item);
                idx++;
            }
            PositionsList.ItemsSource = items;
            StartButton.IsEnabled = VM.AgentVM.Positions.Count > 0;
        }
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (VM?.AgentVM != null)
            {
                var cps = (int)SpeedSlider.Value;
                SpeedValue.Text = $"{cps} CPS";
                VM.AgentVM.StartWithCps(cps);
            }
        }

        private void AgentProfileCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AgentProfileCombo.SelectedItem is ComboBoxItem item && item.Tag is string profileJson)
                LoadPositionsFromProfile(profileJson);
        }

        private void LoadPositionsFromProfile(string json)
        {
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("Positions", out var arr))
                {
                    VM.AgentVM.Positions.Clear();
                    foreach (var el in arr.EnumerateArray())
                    {
                        var x = el.GetProperty("X").GetInt32();
                        var y = el.GetProperty("Y").GetInt32();
                        VM.AgentVM.Positions.Add(new Point(x, y));
                    }
                    VM.AgentVM.HasPositions = VM.AgentVM.Positions.Count > 0;
                    StartButton.IsEnabled = VM.AgentVM.Positions.Count > 0;
                    UpdatePositionList();
                    StatusText.Text = $"✔ Profil yüklendi: {VM.AgentVM.Positions.Count} pozisyon";
                }
            }
            catch { }
        }

        private void SpeedSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (VM.AgentVM != null && SpeedValue != null)
                SpeedValue.Text = $"{(int)e.NewValue} CPS";
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

        private void InitCrosshairPage()
        {
            RefreshCrosshairCombo();
            SyncCrosshairUIFromVM();
            UpdateCrosshairPreview();
        }

        // --- FakeMic ---

        private bool _fakeMicInitialized;
        private PlaylistItem _listeningCardItem;

        private void InitFakeMicPage()
        {
            if (_fakeMicInitialized) return;
            _fakeMicInitialized = true;

            var fvm = VM?.FakeMicVM;
            if (fvm == null) return;

            FakeMicCardsContainer.ItemsSource = fvm.Playlist;
            PreviewKeyDown += FakeMicPage_KeyDown;

            // VB-Cable durumu
            if (fvm.IsVBCableInstalled && fvm.VBCableDeviceIndex >= 0)
            {
                FakeMicStatusText.Text = "VB-Cable kurulu";
                FakeMicVbDownloadBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                FakeMicStatusText.Text = "VB-Cable kurulu değil. Kurulum için tıklayın.";
                FakeMicVbDownloadBtn.Visibility = Visibility.Visible;
            }

            // Sayfa açılış animasyonu
            AnimateFakeMicPageIn();
        }

        private void AnimateFakeMicPageIn()
        {
#if DEBUG
            try
            {
#endif
                var sb1 = new System.Windows.Media.Animation.Storyboard();
                var fade1 = new System.Windows.Media.Animation.DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(250) };
                System.Windows.Media.Animation.Storyboard.SetTarget(fade1, FakeMicSection1);
                System.Windows.Media.Animation.Storyboard.SetTargetProperty(fade1, new PropertyPath(UIElement.OpacityProperty));
                sb1.Children.Add(fade1);

                var sb2 = new System.Windows.Media.Animation.Storyboard();
                sb2.BeginTime = TimeSpan.FromMilliseconds(80);
                var fade2 = new System.Windows.Media.Animation.DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(250) };
                System.Windows.Media.Animation.Storyboard.SetTarget(fade2, FakeMicSection2);
                System.Windows.Media.Animation.Storyboard.SetTargetProperty(fade2, new PropertyPath(UIElement.OpacityProperty));
                sb2.Children.Add(fade2);
                var trans2 = new System.Windows.Media.Animation.DoubleAnimation { From = 12, To = 0, Duration = TimeSpan.FromMilliseconds(250), EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };
                System.Windows.Media.Animation.Storyboard.SetTarget(trans2, FakeMicSection2);
                System.Windows.Media.Animation.Storyboard.SetTargetProperty(trans2, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
                sb2.Children.Add(trans2);

                var sb3 = new System.Windows.Media.Animation.Storyboard();
                sb3.BeginTime = TimeSpan.FromMilliseconds(160);
                var fade3 = new System.Windows.Media.Animation.DoubleAnimation { From = 0, To = 1, Duration = TimeSpan.FromMilliseconds(300) };
                System.Windows.Media.Animation.Storyboard.SetTarget(fade3, FakeMicSection3);
                System.Windows.Media.Animation.Storyboard.SetTargetProperty(fade3, new PropertyPath(UIElement.OpacityProperty));
                sb3.Children.Add(fade3);
                var trans3 = new System.Windows.Media.Animation.DoubleAnimation { From = 12, To = 0, Duration = TimeSpan.FromMilliseconds(300), EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };
                System.Windows.Media.Animation.Storyboard.SetTarget(trans3, FakeMicSection3);
                System.Windows.Media.Animation.Storyboard.SetTargetProperty(trans3, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.Y)"));
                sb3.Children.Add(trans3);

                sb1.Begin();
                sb2.Begin();
                sb3.Begin();
#if DEBUG
            }
            catch { }
#endif
        }

        private void FakeMicVbDownload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start("https://vb-audio.com/Cable/");
            }
            catch { }
        }

        private void FakeMicAddBtn_Click(object sender, RoutedEventArgs e)
        {
            VM?.FakeMicVM?.AddFiles();
        }

        private void FakeMicRemoveBtn_Click(object sender, RoutedEventArgs e)
        {
            var fvm = VM?.FakeMicVM;
            if (fvm == null || !fvm.Playlist.Any()) return;
            if (MessageBox.Show("Tüm dosyalar silinecek. Emin misin?", "Onay",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                while (fvm.Playlist.Any())
                    fvm.RemoveItem(fvm.Playlist[0]);
            }
        }

        private void FakeMicPlayBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is PlaylistItem item)
                VM?.FakeMicVM?.PlayFile(item);
        }

        private void FakeMicBindBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is PlaylistItem item)
            {
                _listeningCardItem = item;
                if (_listeningCardItem != null) _listeningCardItem.IsListening = false;
                item.IsListening = true;
            }
        }

        private void FakeMicPage_KeyDown(object sender, KeyEventArgs e)
        {
            if (_listeningCardItem == null) return;
            var key = e.Key;
            if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt ||
                key == Key.RightAlt || key == Key.LeftShift || key == Key.RightShift ||
                key == Key.LWin || key == Key.RWin)
                return;

            int vk = KeyInterop.VirtualKeyFromKey(key);
            if (_listeningCardItem != null) _listeningCardItem.IsListening = false;
            var item = _listeningCardItem;
            if (item != null) item.HotkeyVk = vk;

            // Re-register all file hotkeys
            UnregisterHotKeys();
            RegisterHotKeys();

            _listeningCardItem = null;
            e.Handled = true;
        }

        private void HandleFakeMicHotkey()
        {
            var fvm = VM?.FakeMicVM;
            if (fvm == null) return;
            if (!fvm.Playlist.Any()) return;
            if (fvm.IsPlaying) fvm.Stop(); else fvm.PlayFile(fvm.Playlist[0]);
        }

        private void CrosshairProfileCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CrosshairProfileCombo.SelectedItem is ComboBoxItem item && item.Tag is string profileName)
            {
                if (VM?.CrosshairVM != null)
                {
                    VM.CrosshairVM.LoadProfileByName(profileName);
                    SyncCrosshairUIFromVM();
                    UpdateCrosshairPreview();
                }
            }
        }

        private void SaveProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (VM?.CrosshairVM == null) return;
            var name = ProfileNameInput.Text?.Trim();
            if (string.IsNullOrEmpty(name)) name = "Yeni Profil";
            VM.CrosshairVM.SelectedProfileName = name;
            SyncCrosshairUIToVM();
            VM.CrosshairVM.SaveProfileCommand.Execute(null);
            RefreshCrosshairCombo();
            UpdateCrosshairPreview();
        }

        private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
        {
            VM.CrosshairVM?.DeleteProfileCommand?.Execute(null);
            RefreshCrosshairCombo();
            SyncCrosshairUIFromVM();
            UpdateCrosshairPreview();
        }

        private void ImportProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Dosyası|*.json",
                Title = "Crosshair Profili İçe Aktar"
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var json = System.IO.File.ReadAllText(dialog.FileName);
                    var imported = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, CrosshairSettings>>(json);
                    if (imported != null && VM?.CrosshairVM != null)
                    {
                        var profiles = VM.CrosshairVM.GetProfiles();
                        foreach (var kv in imported)
                            profiles[kv.Key] = kv.Value;
                        VM.CrosshairVM.SaveProfilesToService();
                        VM.CrosshairVM.RefreshProfileNames();
                        RefreshCrosshairCombo();
                        SyncCrosshairUIFromVM();
                        UpdateCrosshairPreview();
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"İçe aktarma hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportProfileButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "JSON Dosyası|*.json",
                Title = "Crosshair Profili Dışa Aktar",
                FileName = "crosshair_profiles.json"
            };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var profiles = VM?.CrosshairVM?.GetProfiles();
                    if (profiles != null)
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(profiles, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                        System.IO.File.WriteAllText(dialog.FileName, json);
                        MessageBox.Show("Profiller dışa aktarıldı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Dışa aktarma hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void EnableCrosshairButton_Click(object sender, RoutedEventArgs e)
        {
            if (VM?.CrosshairVM == null) return;
            VM.CrosshairVM.ToggleCrosshairCommand.Execute(null);
            if (VM.CrosshairVM.CrosshairEnabled)
            {
                if (_crosshairOverlay == null)
                    _crosshairOverlay = new Views.CrosshairOverlayWindow();
                _crosshairOverlay.UpdateCrosshair(VM.CrosshairVM.CurrentSettings);
                _crosshairOverlay.Show();
                EnableCrosshairButton.Content = "Crosshair Devre Dışı";
            }
            else
            {
                _crosshairOverlay?.Hide();
                EnableCrosshairButton.Content = "Crosshair Etkinleştir";
            }
        }

        private void CrosshairColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string color)
            {
                VM.CrosshairVM?.SetColorCommand?.Execute(color);
                UpdateCrosshairPreview();
            }
        }

        private void CrosshairSlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
        {
            if (VM?.CrosshairVM?.CurrentSettings == null) return;

            if (sender == InnerLengthSlider) { InnerLengthValue.Text = ((int)e.NewValue).ToString(); VM.CrosshairVM.CurrentSettings.InnerLength = (int)e.NewValue; }
            else if (sender == InnerThicknessSlider) { InnerThicknessValue.Text = ((int)e.NewValue).ToString(); VM.CrosshairVM.CurrentSettings.InnerThickness = (int)e.NewValue; }
            else if (sender == InnerGapSlider) { InnerGapValue.Text = ((int)e.NewValue).ToString(); VM.CrosshairVM.CurrentSettings.InnerGap = (int)e.NewValue; }
            else if (sender == OuterLengthSlider) { OuterLengthValue.Text = ((int)e.NewValue).ToString(); VM.CrosshairVM.CurrentSettings.OuterLength = (int)e.NewValue; }
            else if (sender == OuterThicknessSlider) { OuterThicknessValue.Text = ((int)e.NewValue).ToString(); VM.CrosshairVM.CurrentSettings.OuterThickness = (int)e.NewValue; }
            else if (sender == OuterGapSlider) { OuterGapValue.Text = ((int)e.NewValue).ToString(); VM.CrosshairVM.CurrentSettings.OuterGap = (int)e.NewValue; }
            else if (sender == CenterDotSizeSlider) { CenterDotSizeValue.Text = ((int)e.NewValue).ToString(); VM.CrosshairVM.CurrentSettings.CenterDotSize = (int)e.NewValue; }
            else if (sender == OpacitySlider) { OpacityValue.Text = $"{(int)e.NewValue}%"; VM.CrosshairVM.CurrentSettings.Opacity = (int)e.NewValue; }
            else return;

            UpdateCrosshairPreview();
        }

        private void CrosshairCheckbox_Changed(object sender, RoutedEventArgs e)
        {
            if (VM?.CrosshairVM?.CurrentSettings == null) return;

            if (sender == OuterLinesCheckbox)
            {
                OuterLinesPanel.Visibility = OuterLinesCheckbox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                VM.CrosshairVM.CurrentSettings.OuterLines = OuterLinesCheckbox.IsChecked == true;
            }
            else if (sender == CenterDotCheckbox)
            {
                CenterDotPanel.Visibility = CenterDotCheckbox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                VM.CrosshairVM.CurrentSettings.CenterDot = CenterDotCheckbox.IsChecked == true;
            }
            UpdateCrosshairPreview();
        }

        private void SyncCrosshairUIFromVM()
        {
            if (VM?.CrosshairVM?.CurrentSettings == null) return;
            var s = VM.CrosshairVM.CurrentSettings;
            InnerLengthSlider.Value = s.InnerLength;
            InnerThicknessSlider.Value = s.InnerThickness;
            InnerGapSlider.Value = s.InnerGap;
            OuterLinesCheckbox.IsChecked = s.OuterLines;
            OuterLengthSlider.Value = s.OuterLength;
            OuterThicknessSlider.Value = s.OuterThickness;
            OuterGapSlider.Value = s.OuterGap;
            CenterDotCheckbox.IsChecked = s.CenterDot;
            CenterDotSizeSlider.Value = s.CenterDotSize;
            OpacitySlider.Value = s.Opacity;
            if (!string.IsNullOrEmpty(s.Color))
                VM.CrosshairVM.CurrentSettings.Color = s.Color;
        }

        private void SyncCrosshairUIToVM()
        {
            if (VM?.CrosshairVM?.CurrentSettings == null) return;
            var s = VM.CrosshairVM.CurrentSettings;
            s.InnerLength = (int)InnerLengthSlider.Value;
            s.InnerThickness = (int)InnerThicknessSlider.Value;
            s.InnerGap = (int)InnerGapSlider.Value;
            s.OuterLines = OuterLinesCheckbox.IsChecked == true;
            s.OuterLength = (int)OuterLengthSlider.Value;
            s.OuterThickness = (int)OuterThicknessSlider.Value;
            s.OuterGap = (int)OuterGapSlider.Value;
            s.CenterDot = CenterDotCheckbox.IsChecked == true;
            s.CenterDotSize = (int)CenterDotSizeSlider.Value;
            s.Opacity = (int)OpacitySlider.Value;
        }

        private void RefreshCrosshairCombo()
        {
            CrosshairProfileCombo.Items.Clear();
            if (VM?.CrosshairVM?.ProfileNames == null) return;
            foreach (var name in VM.CrosshairVM.ProfileNames)
            {
                var item = new ComboBoxItem { Content = name, Tag = name };
                CrosshairProfileCombo.Items.Add(item);
                if (name == VM.CrosshairVM.SelectedProfileName)
                    CrosshairProfileCombo.SelectedItem = item;
            }
            if (CrosshairProfileCombo.SelectedItem == null && CrosshairProfileCombo.Items.Count > 0)
                CrosshairProfileCombo.SelectedIndex = 0;
        }

        private void UpdateCrosshairPreview()
        {
            if (CrosshairPreview == null || VM?.CrosshairVM?.CurrentSettings == null) return;
            CrosshairPreview.Children.Clear();

            var s = VM.CrosshairVM.CurrentSettings;
            var color = ParseColor(s.Color);
            double cx = CrosshairPreview.Width / 2;
            double cy = CrosshairPreview.Height / 2;
            double maxAllowed = Math.Min(cx, cy) - 4;

            // Calculate required extent for inner + outer lines
            int innerSpan = s.InnerGap + s.InnerLength;
            int outerSpan = s.OuterLines ? s.OuterGap + s.OuterLength : 0;
            int totalSpan = innerSpan + outerSpan;
            int dotSpan = s.CenterDot ? s.CenterDotSize / 2 : 0;
            int needed = Math.Max(totalSpan, dotSpan);
            double scale = needed > maxAllowed ? maxAllowed / needed : 1.0;

            byte alpha = (byte)Math.Clamp(s.Opacity * 255 / 100, 0, 255);
            var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));

            // Inner lines
            double innerLen = s.InnerLength * scale;
            double innerThick = Math.Max(1, s.InnerThickness * scale);
            double innerGap = s.InnerGap * scale;

            AddLine(cx + innerGap, cy, cx + innerGap + innerLen, cy, innerThick, brush);
            AddLine(cx - innerGap - innerLen, cy, cx - innerGap, cy, innerThick, brush);
            AddLine(cx, cy + innerGap, cx, cy + innerGap + innerLen, innerThick, brush);
            AddLine(cx, cy - innerGap - innerLen, cx, cy - innerGap, innerThick, brush);

            // Outer lines
            if (s.OuterLines)
            {
                double outerLen = s.OuterLength * scale;
                double outerThick = Math.Max(1, s.OuterThickness * scale);
                double outerGap = s.OuterGap * scale;
                double startGap = innerGap + innerLen + outerGap;

                AddLine(cx + startGap, cy, cx + startGap + outerLen, cy, outerThick, brush);
                AddLine(cx - startGap - outerLen, cy, cx - startGap, cy, outerThick, brush);
                AddLine(cx, cy + startGap, cx, cy + startGap + outerLen, outerThick, brush);
                AddLine(cx, cy - startGap - outerLen, cx, cy - startGap, outerThick, brush);
            }

            // Center dot
            if (s.CenterDot)
            {
                double dotSize = Math.Max(1, s.CenterDotSize * scale);
                var dot = new Ellipse
                {
                    Width = dotSize,
                    Height = dotSize,
                    Fill = brush
                };
                Canvas.SetLeft(dot, cx - dotSize / 2.0);
                Canvas.SetTop(dot, cy - dotSize / 2.0);
                CrosshairPreview.Children.Add(dot);
            }

            // Update overlay if active
            if (_crosshairOverlay?.IsVisible == true)
                _crosshairOverlay.UpdateCrosshair(s);
        }

        private void AddLine(double x1, double y1, double x2, double y2, double thickness, Brush brush)
        {
            var line = new System.Windows.Shapes.Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = brush,
                StrokeThickness = thickness
            };
            CrosshairPreview.Children.Add(line);
        }

        private Color ParseColor(string color)
        {
            return color?.ToLower() switch
            {
                "red" => Color.FromRgb(0xFF, 0x46, 0x55),
                "green" => Color.FromRgb(0x00, 0xFF, 0x41),
                "blue" => Color.FromRgb(0x00, 0xD4, 0xFF),
                "yellow" => Color.FromRgb(0xFF, 0xFF, 0x00),
                "purple" => Color.FromRgb(0xFF, 0x00, 0xFF),
                "white" => Color.FromRgb(0xFF, 0xFF, 0xFF),
                "cyan" => Color.FromRgb(0x00, 0xFF, 0xFF),
                "orange" => Color.FromRgb(0xFF, 0x80, 0x00),
                _ => Color.FromRgb(0xFF, 0x46, 0x55)
            };
        }

        private void HotkeyCombo_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e) { if (VM != null) VM.SaveConfig(); }

        private void LightTheme_Click(object sender, RoutedEventArgs e) { if (VM != null) { VM.IsDarkMode = false; VM.SaveConfig(); } }
        private void DarkTheme_Click(object sender, RoutedEventArgs e) { if (VM != null) { VM.IsDarkMode = true; VM.SaveConfig(); } }
        private void Turkish_Click(object sender, RoutedEventArgs e) { if (VM != null) { VM.SetLanguageCommand?.Execute("TR"); } }
        private void English_Click(object sender, RoutedEventArgs e) { if (VM != null) { VM.SetLanguageCommand?.Execute("EN"); } }

        private void Instagram_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => OpenUrl("https://www.instagram.com/autoclicker.g/");
        private void Youtube_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => OpenUrl("https://www.youtube.com/channel/UCp2HkORdCOwCqsthrbrFpRg/");
        private void Tiktok_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => OpenUrl("https://www.tiktok.com/@autoclicker.v");

        private static readonly string[] AllowedDomains = {
            "instagram.com",
            "www.instagram.com",
            "youtube.com",
            "www.youtube.com",
            "tiktok.com",
            "www.tiktok.com",
            "vb-audio.com",
            "www.vb-audio.com",
            "wa.me"
        };

        private static bool IsUrlAllowed(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            try
            {
                var uri = new Uri(url);
                return uri.Scheme == "https" &&
                       AllowedDomains.Any(d => uri.Host.EndsWith(d, StringComparison.OrdinalIgnoreCase));
            }
            catch { return false; }
        }

        private void OpenUrl(string url)
        {
            if (!IsUrlAllowed(url)) return;
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = url, UseShellExecute = true }); }
            catch { }
        }

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
            // Üst profil avatar
            if (profil?.GecerliMi == true)
            {
                if (!string.IsNullOrEmpty(profil.CardSmallUrl))
                {
                    try
                    {
                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.UriSource = new Uri(profil.CardSmallUrl);
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnDemand;
                        bmp.EndInit();
                        bmp.DownloadCompleted += (s, e) =>
                        {
                            RightBarAvatarImg.Visibility = Visibility.Visible;
                        };
                        RightBarAvatarImg.Source = bmp;
                    }
                    catch { }
                }
                var rankIkon = RankIkonHelper.RankIkon(profil.Rutbe);
                if (rankIkon != null)
                {
                    SettingsRankIkon.Source = rankIkon;
                    SettingsRankIkon.Visibility = Visibility.Visible;
                }
                else
                {
                    SettingsRankIkon.Visibility = Visibility.Collapsed;
                }
            }
        }

        // ─── ÜST PROFİL AVATAR + DROPDOWN ──────────────────────────────────

        private void RightBarAvatar_Click(object sender, MouseButtonEventArgs e)
        {
            if (ProfileDropdown.Visibility == Visibility.Visible)
                ProfilPanelKapat();
            else
                ProfilPanelAc();
        }

        private void ProfilPanelAc()
        {
            ProfileOverlay.Visibility = Visibility.Visible;
            ProfileDropdown.Visibility = Visibility.Visible;
            ProfileDropdown.Opacity = 0;
            var fadeAnim = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ProfileDropdown.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

            if (ProfileDropdown.RenderTransform is TranslateTransform tt)
            {
                tt.X = 20;
                var slideAnim = new DoubleAnimation(20, 0, new Duration(TimeSpan.FromMilliseconds(200)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                tt.BeginAnimation(TranslateTransform.XProperty, slideAnim);
            }
        }

        private void ProfilPanelKapat()
        {
            var fadeAnim = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(150)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fadeAnim.Completed += (s, e) =>
            {
                ProfileDropdown.Visibility = Visibility.Collapsed;
                ProfileOverlay.Visibility = Visibility.Collapsed;
            };
            ProfileDropdown.BeginAnimation(UIElement.OpacityProperty, fadeAnim);

            if (ProfileDropdown.RenderTransform is TranslateTransform tt)
            {
                var slideAnim = new DoubleAnimation(0, 20, new Duration(TimeSpan.FromMilliseconds(150)))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };
                tt.BeginAnimation(TranslateTransform.XProperty, slideAnim);
            }
        }

        private void ProfileOverlay_Click(object sender, MouseButtonEventArgs e)
        {
            ProfilPanelKapat();
        }

        // ─── BİLDİRİM ZİLİ + DROPDOWN ─────────────────────────────────

        private Storyboard _pulseStoryboard;
        private Storyboard _taramaStoryboard;

        private void BildirimZili_Click(object sender, MouseButtonEventArgs e)
        {
            if (BildirimDropdown.Visibility == Visibility.Visible)
                BildirimPanelKapat();
            else
                BildirimPanelAc();
        }

        private void BildirimPanelAc()
        {
            try
            {
                // Profil panelini kapat (çakışmasın)
                if (ProfileDropdown.Visibility == Visibility.Visible)
                    ProfilPanelKapat();

                // Panel içeriğini güncelle
                PanelGuncelleBtn.Visibility = Visibility.Collapsed;
                PanelErrorArea.Visibility = Visibility.Collapsed;
                PanelProgressTrack.Visibility = Visibility.Collapsed;

                var bildirimVar = VM != null && VM.BildirimVar;
                var guncellemeVar = VM != null && VM.UpdateAvailable;

                if (guncellemeVar)
                {
                    // Güncelleme var
                    PanelNoUpdate.Visibility = Visibility.Collapsed;
                    PanelBildirim.Visibility = Visibility.Collapsed;
                    PanelUpdate.Visibility = Visibility.Visible;
                    PanelGuncelleBtn.Visibility = Visibility.Visible;
                    PanelVersionText.Text = $"v{VM.RemoteVersion}";
                    PanelReleaseNotes.Text = VM.ReleaseNotes ?? "";
                }
                else if (bildirimVar)
                {
                    PanelNoUpdate.Visibility = Visibility.Collapsed;
                    PanelBildirim.Visibility = Visibility.Visible;
                    PanelBildirimBaslik.Text = VM.BildirimBaslik;
                    PanelBildirimMesaj.Text = VM.BildirimMesaj;
                    PanelUpdate.Visibility = Visibility.Collapsed;
                }
                else
                {
                    PanelBildirim.Visibility = Visibility.Collapsed;
                    PanelUpdate.Visibility = Visibility.Collapsed;
                    PanelNoUpdate.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                PanelNoUpdate.Visibility = Visibility.Visible;
                PanelUpdate.Visibility = Visibility.Collapsed;
                PanelBildirim.Visibility = Visibility.Collapsed;
            }

            BildirimDropdownOverlay.Visibility = Visibility.Visible;
            BildirimDropdown.Visibility = Visibility.Visible;
            BildirimDropdown.Opacity = 0;
            var fadeAnim = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(200)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BildirimDropdown.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        }

        private void BildirimPanelKapat()
        {
            var fadeAnim = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(150)))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fadeAnim.Completed += (s, e) =>
            {
                BildirimDropdown.Visibility = Visibility.Collapsed;
                BildirimDropdownOverlay.Visibility = Visibility.Collapsed;
            };
            BildirimDropdown.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
        }

        private void BildirimDropdownOverlay_Click(object sender, MouseButtonEventArgs e)
        {
            BildirimPanelKapat();
        }

        private void GuncellemeDurumGuncelle(string status)
        {
            switch (status)
            {
                case "Kuruluyor...":
                    PanelGuncelleBtnText.Text = "Kuruluyor...";
                    break;
                case "Yeniden başlatılıyor...":
                    PanelGuncelleBtnText.Text = "Yeniden başlatılıyor...";
                    break;
                case "İndirme Başarısız":
                    PanelGuncelleBtnText.Text = "İndirme Başarısız";
                    PanelErrorArea.Visibility = Visibility.Visible;
                    PanelErrorText.Text = "İndirme Başarısız";
                    break;
                case "Otomatik güncelleme başarısız":
                    PanelGuncelleBtnText.Text = "Güncelleme başarısız";
                    PanelErrorArea.Visibility = Visibility.Visible;
                    PanelErrorText.Text = "Otomatik güncelleme başarısız";
                    break;
                default:
                    if (string.IsNullOrEmpty(status) && !VM.IsDownloading)
                    {
                        PanelProgressTrack.Visibility = Visibility.Collapsed;
                        PanelGuncelleBtnText.Text = "Güncelle";
                    }
                    break;
            }
        }

        private async void PanelGuncelleBtn_Click(object sender, MouseButtonEventArgs e)
        {
            if (VM.IsDownloading) return;

            PanelGuncelleBtnText.Text = "İndiriliyor... %0";
            PanelProgressTrack.Visibility = Visibility.Visible;
            PanelProgressFill.Width = 0;
            PanelProgressTrack.UpdateLayout();
            PanelErrorArea.Visibility = Visibility.Collapsed;

            await VM.GuncellemeIndirCommand.ExecuteAsync(null);
        }

        private void PanelRetry_Click(object sender, MouseButtonEventArgs e)
        {
            VM.RetryDownload();
            PanelErrorArea.Visibility = Visibility.Collapsed;
            PanelGuncelleBtnText.Text = "Güncelle";
            PanelProgressTrack.Visibility = Visibility.Collapsed;
        }

        // Red dot pulse animasyonu
        private void BaslatPulseAnimasyonu()
        {
            if (_pulseStoryboard != null) return;
            _pulseStoryboard = new Storyboard();
            _pulseStoryboard.RepeatBehavior = RepeatBehavior.Forever;
            _pulseStoryboard.AutoReverse = true;

            var opacityAnim = new DoubleAnimation(0.4, 1.0, new Duration(TimeSpan.FromSeconds(2)))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(opacityAnim, BildirimNokta);
            Storyboard.SetTargetProperty(opacityAnim, new PropertyPath("Opacity"));
            _pulseStoryboard.Children.Add(opacityAnim);

            var scaleXAnim = new DoubleAnimation(0.9, 1.1, new Duration(TimeSpan.FromSeconds(2)))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(scaleXAnim, BildirimNokta);
            Storyboard.SetTargetProperty(scaleXAnim, new PropertyPath("RenderTransform.ScaleX"));
            _pulseStoryboard.Children.Add(scaleXAnim);

            var scaleYAnim = new DoubleAnimation(0.9, 1.1, new Duration(TimeSpan.FromSeconds(2)))
            {
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(scaleYAnim, BildirimNokta);
            Storyboard.SetTargetProperty(scaleYAnim, new PropertyPath("RenderTransform.ScaleY"));
            _pulseStoryboard.Children.Add(scaleYAnim);

            _pulseStoryboard.Begin();
        }

        private void DurdurPulseAnimasyonu()
        {
            _pulseStoryboard?.Stop();
            _pulseStoryboard = null;
        }

        private void ProfileLogout_Click(object sender, RoutedEventArgs e)
        {
            ProfilPanelKapat();
            VM.CikisYap();
            GuncelleHesapBilgisi();
            RightBarAvatarImg.Source = null;
            RightBarAvatarImg.Visibility = Visibility.Collapsed;
            // Login overlay fade in
            if (LoginOverlay != null)
            {
                LoginOverlay.Opacity = 0;
                LoginOverlay.Visibility = Visibility.Visible;
                var anim = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(300)));
                LoginOverlay.BeginAnimation(UIElement.OpacityProperty, anim);
            }
        }

        private void EquipSaveBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var data = new Dictionary<string, string>
                {
                    ["mouse"] = EquipMouse.Text?.Trim() ?? "",
                    ["keyboard"] = EquipKeyboard.Text?.Trim() ?? "",
                    ["monitor"] = EquipMonitor.Text?.Trim() ?? "",
                    ["headset"] = EquipHeadset.Text?.Trim() ?? "",
                    ["mousepad"] = EquipMousepad.Text?.Trim() ?? ""
                };
                var json = System.Text.Json.JsonSerializer.Serialize(data, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Klyze", "ekipman.json"), json);
                ShowEquipSavedView(data);
            }
            catch { }
        }

        private void EquipEditBtn_Click(object sender, RoutedEventArgs e)
        {
            EquipFormPanel.Visibility = Visibility.Visible;
            EquipSavedPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowEquipSavedView(Dictionary<string, string> data)
        {
            EquipFormPanel.Visibility = Visibility.Collapsed;
            SavedMouse.Text = data.GetValueOrDefault("mouse", "-");
            SavedKeyboard.Text = data.GetValueOrDefault("keyboard", "-");
            SavedMonitor.Text = data.GetValueOrDefault("monitor", "-");
            SavedHeadset.Text = data.GetValueOrDefault("headset", "-");
            SavedMousepad.Text = data.GetValueOrDefault("mousepad", "-");
            EquipSavedPanel.Visibility = Visibility.Visible;
        }

        private void UpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            if (VM != null && !string.IsNullOrEmpty(VM.DownloadUrl) && !VM.IsDownloading)
                _ = VM.GuncellemeIndirAsync();
        }

    }

}