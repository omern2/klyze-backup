using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
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

        // ─── Güncelleme Durumu (Firestore'dan gelen) ──────────────────────────────

        private bool _updateAvailable;
        public bool UpdateAvailable
        {
            get => _updateAvailable;
            set => SetProperty(ref _updateAvailable, value);
        }

        private string _localVersion = "";
        public string LocalVersion
        {
            get => _localVersion;
            set => SetProperty(ref _localVersion, value);
        }

        private string _remoteVersion = "";
        public string RemoteVersion
        {
            get => _remoteVersion;
            set => SetProperty(ref _remoteVersion, value);
        }

        private string _downloadUrl = "";
        public string DownloadUrl
        {
            get => _downloadUrl;
            set => SetProperty(ref _downloadUrl, value);
        }

        private string _releaseNotes = "";
        public string ReleaseNotes
        {
            get => _releaseNotes;
            set => SetProperty(ref _releaseNotes, value);
        }

        // ─── İndirme Durumu ──────────────────────────────────────────────────────

        private bool _isDownloading;
        public bool IsDownloading
        {
            get => _isDownloading;
            set => SetProperty(ref _isDownloading, value);
        }

        private int _downloadProgress;
        public int DownloadProgress
        {
            get => _downloadProgress;
            set
            {
                if (SetProperty(ref _downloadProgress, value))
                    OnPropertyChanged(nameof(DownloadProgressText));
            }
        }
        public string DownloadProgressText => $"İndiriliyor... %{DownloadProgress}";

        private string _downloadStatus = "";
        public string DownloadStatus
        {
            get => _downloadStatus;
            set => SetProperty(ref _downloadStatus, value);
        }

        public bool IsInstalling => _downloadPhase >= 2;
        private int _downloadPhase; // 0=idle, 1=downloading, 2=extracting, 3=done

        // ─── Yerel Sürümü Oku (appsettings.json) ─────────────────────────────────

        public void YerelSurumuOku()
        {
            try
            {
                var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (System.IO.File.Exists(path))
                {
                    var json = System.IO.File.ReadAllText(path);
                    var obj = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (obj != null && obj.ContainsKey("version"))
                        LocalVersion = obj["version"] ?? "";
                }
                if (string.IsNullOrEmpty(LocalVersion))
                    LocalVersion = "3.9.0";
            }
            catch
            {
                LocalVersion = "3.9.0";
            }
        }

        // ─── Firestore Sürüm Kontrolü ────────────────────────────────────────────

        public void FirestoreGuncellemeKontrol(AppVersionDoc doc)
        {
            if (doc == null) return;
            if (string.IsNullOrEmpty(doc.version) || string.IsNullOrEmpty(doc.downloadUrl)) return;

            if (!IsNewerVersion(LocalVersion, doc.version)) return;

            RemoteVersion = doc.version;
            DownloadUrl = doc.downloadUrl;
            ReleaseNotes = doc.releaseNotes ?? "";
            UpdateAvailable = true;
        }

        public static bool IsNewerVersion(string current, string remote)
        {
            try
            {
                var c = current.Split('.').Select(int.Parse).ToArray();
                var r = remote.Split('.').Select(int.Parse).ToArray();
                for (int i = 0; i < Math.Max(c.Length, r.Length); i++)
                {
                    int cVal = i < c.Length ? c[i] : 0;
                    int rVal = i < r.Length ? r[i] : 0;
                    if (rVal > cVal) return true;
                    if (rVal < cVal) return false;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        // ─── Güncellemeyi İndir + Kur ────────────────────────────────────────────

        [RelayCommand]
        public async Task GuncellemeIndirAsync()
        {
            if (IsDownloading || string.IsNullOrEmpty(DownloadUrl)) return;

            IsDownloading = true;
            DownloadProgress = 0;
            DownloadStatus = "İndiriliyor... %0";
            _downloadPhase = 1;

            try
            {
                // Aşama 1 — İndir
                var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "klyze_update");
                System.IO.Directory.CreateDirectory(tempDir);
                var zipPath = System.IO.Path.Combine(tempDir, "update.zip");

                var progress = new Progress<int>(p =>
                {
                    DownloadProgress = p;
                    DownloadStatus = $"İndiriliyor... %{p}";
                });

                var firebase = App.Firebase;
                if (firebase == null) throw new Exception("Firebase servisi kullanılamıyor.");

                var ok = await firebase.DownloadUpdateZipAsync(DownloadUrl, zipPath, progress);
                if (!ok) throw new Exception("Dosya indirilemedi.");

                // Aşama 2 — Zip'ten çıkar
                _downloadPhase = 2;
                DownloadStatus = "Kuruluyor...";
                var extractDir = System.IO.Path.Combine(tempDir, "extracted");
                if (System.IO.Directory.Exists(extractDir))
                    System.IO.Directory.Delete(extractDir, true);
                ZipFile.ExtractToDirectory(zipPath, extractDir);

                // Aşama 3 — PowerShell ile değiştir + yeniden başlat
                _downloadPhase = 3;
                DownloadStatus = "Yeniden başlatılıyor...";
                StartUpdateAndExit(extractDir);
            }
            catch (Exception ex)
            {
                DownloadStatus = "İndirme Başarısız";
                _downloadPhase = 0;
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log"),
                    $"\n[UPDATE] {ex.Message}\n{ex.StackTrace}\n");
                await Task.Delay(3000);
                DownloadStatus = "";
                IsDownloading = false;
            }
        }

        private void StartUpdateAndExit(string extractedPath)
        {
            try
            {
                var appPath = System.IO.Path.GetDirectoryName(Environment.ProcessPath);
                var exeName = System.IO.Path.GetFileName(Environment.ProcessPath);
                var scriptContent = GetUpdateScriptContent();
                var scriptDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "klyze_update");
                System.IO.Directory.CreateDirectory(scriptDir);
                var scriptPath = System.IO.Path.Combine(scriptDir, "update.ps1");
                System.IO.File.WriteAllText(scriptPath, scriptContent, System.Text.Encoding.Unicode);

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\" " +
                                $"-SourcePath \"{extractedPath}\" -TargetPath \"{appPath}\" -ExeName \"{exeName}\"",
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = true
                };
                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc != null)
                {
                    Task.Delay(500).ContinueWith(_ =>
                    {
                        Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
                    });
                }
                else
                {
                    throw new Exception("Process.Start returned null");
                }
            }
            catch (Exception ex)
            {
                DownloadStatus = "Otomatik güncelleme başarısız";
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log"),
                    $"\n[UPDATE START] {ex.Message}\n");
                IsDownloading = false;
            }
        }

        private static string GetUpdateScriptContent()
        {
            return @"param(
    [string]$SourcePath,
    [string]$TargetPath,
    [string]$ExeName
)

$waited = 0
do {
    Start-Sleep -Milliseconds 500
    $waited += 500
    $proc = Get-Process -Name ($ExeName -replace '\.exe$','') -ErrorAction SilentlyContinue
} while ($proc -and $waited -lt 10000)

$backupData = ""$env:TEMP\klyze_backup_data""
if (Test-Path ""$TargetPath\data"") {
    if (Test-Path $backupData) { Remove-Item $backupData -Recurse -Force }
    Copy-Item ""$TargetPath\data"" $backupData -Recurse -Force
}

Get-ChildItem $TargetPath -Exclude ""data"" | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

Copy-Item ""$SourcePath\*"" $TargetPath -Recurse -Force

if (Test-Path $backupData) {
    if (Test-Path ""$TargetPath\data"") { Remove-Item ""$TargetPath\data"" -Recurse -Force }
    Copy-Item $backupData ""$TargetPath\data"" -Recurse -Force
    Remove-Item $backupData -Recurse -Force
}

Remove-Item ""$env:TEMP\klyze_update"" -Recurse -Force -ErrorAction SilentlyContinue

Start-Process ""$TargetPath\$ExeName""
";
        }

        public void RetryDownload()
        {
            DownloadStatus = "";
            DownloadProgress = 0;
            IsDownloading = false;
            _downloadPhase = 0;
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
