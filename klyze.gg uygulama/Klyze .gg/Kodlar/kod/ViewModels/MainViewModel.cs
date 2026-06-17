using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValorantAutoClicker.Models;
using ValorantAutoClicker.Services;

namespace ValorantAutoClicker.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ConfigService _configService;
        private readonly CrosshairService _crosshairService;
        private readonly ClickingService _clickingService;
        private readonly AfkService _afkService;
        private readonly SpamService _spamService;
        private readonly UserService _userService;
        // Giriş ekranı gösterilsin mi?
        private bool _girisEkraniGoster;
        public bool GirisEkraniGoster
        {
            get => _girisEkraniGoster;
            set => SetProperty(ref _girisEkraniGoster, value);
        }

        // Giriş yapıldı eventi — MainWindow dinler
        public event Action GirisYapildi;

        private PageType _currentPage = PageType.Home;
        public PageType CurrentPage
        {
            get => _currentPage;
            set => SetProperty(ref _currentPage, value);
        }

        private bool _isDarkMode = true;
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (SetProperty(ref _isDarkMode, value))
                {
                    OnPropertyChanged(nameof(IsLightMode));
                    ThemeChanged?.Invoke();
                }
            }
        }

        public bool IsLightMode => !IsDarkMode;

        private string _language = "TR";
        public string Language
        {
            get => _language;
            set
            {
                if (SetProperty(ref _language, value))
                    LanguageChanged?.Invoke();
            }
        }

        public ObservableCollection<AppBildirim> Bildirimler { get; } = new();

        [ObservableProperty]
        private string _sonGuncellemeSurumu = "";

        [ObservableProperty]
        private string _sonGuncellemeBasligi = "";

        [ObservableProperty]
        private string _sonGuncellemeNotlari = "";

        [ObservableProperty]
        private string _sonGuncellemeDosyaUrl = "";

        public bool GuncellemeVar => !string.IsNullOrEmpty(SonGuncellemeDosyaUrl);

        private int _okunmamisBildirimSayisi;
        public int OkunmamisBildirimSayisi
        {
            get => _okunmamisBildirimSayisi;
            set
            {
                if (SetProperty(ref _okunmamisBildirimSayisi, value))
                    OnPropertyChanged(nameof(HasUnreadNotifications));
            }
        }
        public bool HasUnreadNotifications => OkunmamisBildirimSayisi > 0;

        public void AddGuncellemeBildirimi(AppGuncelleme guncelleme)
        {
            SonGuncellemeSurumu = guncelleme.Version ?? "";
            SonGuncellemeBasligi = guncelleme.Title ?? "";
            SonGuncellemeNotlari = guncelleme.Notes ?? "";
            SonGuncellemeDosyaUrl = guncelleme.DosyaUrl ?? "";

            var bildirim = new AppBildirim
            {
                Baslik = guncelleme.Title ?? "Güncelleme",
                Mesaj = guncelleme.Notes ?? "Yeni güncelleme mevcut.",
                Tip = BildirimTipi.Guncelleme,
                Tarih = DateTime.Now,
                Guncelleme = guncelleme
            };
            Bildirimler.Insert(0, bildirim);
            OkunmamisBildirimSayisi = Bildirimler.Count(b => !b.Okundu);
        }

        [RelayCommand]
        public async Task GuncellemeIndirAsync(AppBildirim bildirim = null)
        {
            var guncelleme = bildirim?.Guncelleme;
            if (guncelleme == null)
            {
                if (Bildirimler.Count > 0)
                    guncelleme = Bildirimler[0].Guncelleme;
                if (guncelleme == null || string.IsNullOrEmpty(guncelleme.DosyaUrl))
                    return;
            }

            var firebase = App.Firebase;
            if (firebase == null) return;

            var indi = await firebase.GuncellemeDosyayiIndirAsync(guncelleme);
            if (!indi) return;

            var exePath = Environment.ProcessPath;
            var exeDir = System.IO.Path.GetDirectoryName(exePath);
            var logFile = System.IO.Path.Combine(exeDir ?? ".", "guncelleme.log");
            var currentPid = Environment.ProcessId;
            var cmd = $"Start-Sleep -Seconds 5; " +
                      $"Stop-Process -Id {currentPid} -Force; " +
                      $"Move-Item -LiteralPath '{exeDir}\\Klyze.exe.new' -Destination '{exePath}' -Force *> '{logFile}'; " +
                      $"if ($?) {{ Start-Process -FilePath '{exePath}' -WorkingDirectory '{exeDir}' }} " +
                      $"else {{ 'MOVE FAILED' | Out-File -LiteralPath '{logFile}' -Append }}";

            MessageBox.Show($"Yeni sürüm (v{guncelleme.Version}) indirildi. Uygulama yeniden başlatılıyor...",
                "Güncelleme", MessageBoxButton.OK, MessageBoxImage.Information);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-WindowStyle Hidden -ExecutionPolicy Bypass -Command \"{cmd}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            await Task.Delay(1500);
            Application.Current.Shutdown();
        }

        public void TumBildirimleriOkunduYap()
        {
            foreach (var b in Bildirimler)
                b.Okundu = true;
            OkunmamisBildirimSayisi = 0;
        }

        private string _currentGame = "VALORANT";
        public string CurrentGame
        {
            get => _currentGame;
            set => SetProperty(ref _currentGame, value);
        }

        private bool _sidebarExpanded;
        public bool SidebarExpanded
        {
            get => _sidebarExpanded;
            set => SetProperty(ref _sidebarExpanded, value);
        }

        // Events for view interaction
        public event Action<PageType> PageChanged;
        public event Action ThemeChanged;
        public event Action LanguageChanged;
        public event Action<string> StatusMessage;

        public IRelayCommand<PageType> NavigateCommand { get; }
        public IRelayCommand ToggleSidebarCommand { get; }
        public IRelayCommand ToggleThemeCommand { get; }
        public IRelayCommand<string> SetLanguageCommand { get; }

        public MainViewModel(
            ConfigService configService,
            CrosshairService crosshairService,
            ClickingService clickingService,
            AfkService afkService,
            SpamService spamService)
        {
            _configService = configService;
            _crosshairService = crosshairService;
            _clickingService = clickingService;
            _afkService = afkService;
            _spamService = spamService;
            _userService = new UserService();
            var _henrikApi = new HenrikApiService();

            NavigateCommand = new RelayCommand<PageType>(NavigateTo);
            ToggleSidebarCommand = new RelayCommand(ToggleSidebar);
            ToggleThemeCommand = new RelayCommand(ToggleTheme);
            SetLanguageCommand = new RelayCommand<string>(SetLanguage);

            // Load config
            _configService.Load();
            IsDarkMode = _configService.Config.IsDarkMode;
            Language = _configService.Config.Language;

            // Initialize child viewmodels
            AgentVM = new AgentViewModel(_configService, _clickingService, status => StatusMessage?.Invoke(status));
            AfkVM = new AfkViewModel(_configService, _afkService);
            SpamVM = new SpamViewModel(_configService, _spamService);
            CrosshairVM = new CrosshairViewModel(_configService, _crosshairService);
            FakeMicVM = new FakeMicViewModel();
            SettingsVM = new SettingsViewModel(_configService);
            ValorantVM = new ValorantViewModel(_configService, status => StatusMessage?.Invoke(status));
            HomeVM = new HomeViewModel(_userService);
            PlayVM = new PlayViewModel(_userService);
            AnalizVM = new AnalizViewModel(_userService);

            // Login VM
            LoginVM = new LoginViewModel(_userService, _henrikApi);
            LoginVM.GirisBasarili += OnGirisBasarili;

            // Giriş durumunu kontrol et
            GirisEkraniGoster = !_userService.GirisYapilmisMi();
        }

        public AgentViewModel AgentVM { get; }
        public AfkViewModel AfkVM { get; }
        public SpamViewModel SpamVM { get; }
        public CrosshairViewModel CrosshairVM { get; }
        public FakeMicViewModel FakeMicVM { get; }
        public SettingsViewModel SettingsVM { get; }
        public ValorantViewModel ValorantVM { get; }
        public HomeViewModel HomeVM { get; }
        public PlayViewModel PlayVM { get; }
        public AnalizViewModel AnalizVM { get; }
        public LoginViewModel LoginVM { get; }
        public UserService UserService => _userService;
        public ConfigService ConfigService => _configService;

        private void OnGirisBasarili()
        {
            GirisEkraniGoster = false;
            PlayVM?.YenidenYukle();
            HomeVM?.VeriYukle();
            _ = AnalizVM?.YukleAsync();
            GirisYapildi?.Invoke();
        }

        public void CikisYap()
        {
            _userService.CikisYap();
            GirisEkraniGoster = true;
            PlayVM?.Sifirla();
            HomeVM?.VeriYukle();
        }

        private void NavigateTo(PageType page)
        {
            CurrentPage = page;
            PageChanged?.Invoke(page);
        }

        private void ToggleSidebar() => SidebarExpanded = !SidebarExpanded;

        private void ToggleTheme()
        {
            IsDarkMode = !IsDarkMode;
            _configService.Config.IsDarkMode = IsDarkMode;
            _configService.Save();
        }

        private void SetLanguage(string lang)
        {
            Language = lang;
            _configService.Config.Language = lang;
            _configService.Save();
        }

        public void SaveConfig()
        {
            // Sync from viewmodels
            AgentVM.SyncToConfig();
            AfkVM.SyncToConfig();
            SpamVM.SyncToConfig();
            _configService.Config.IsDarkMode = IsDarkMode;
            _configService.Config.Language = Language;
            _configService.Save();
        }

        public void LoadConfig()
        {
            _configService.Load();
            IsDarkMode = _configService.Config.IsDarkMode;
            Language = _configService.Config.Language;
            AgentVM.LoadFromConfig();
            AfkVM.LoadFromConfig();
            SpamVM.LoadFromConfig();
            HomeVM?.VeriYukle();
        }
    }
}
