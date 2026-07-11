using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ValorantAutoClicker.Helpers;
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

        // ─── Duyuru / Bildirim mesajları ─────────────────────────────────────────

        private string _bildirimBaslik = "";
        public string BildirimBaslik
        {
            get => _bildirimBaslik;
            set => SetProperty(ref _bildirimBaslik, value);
        }

        private string _bildirimMesaj = "";
        public string BildirimMesaj
        {
            get => _bildirimMesaj;
            set => SetProperty(ref _bildirimMesaj, value);
        }

        public bool BildirimVar => !string.IsNullOrEmpty(BildirimBaslik) && !string.IsNullOrEmpty(BildirimMesaj);

        private string _releaseNotes = "";
        public string ReleaseNotes
        {
            get => _releaseNotes;
            set => SetProperty(ref _releaseNotes, value);
        }

        // ─── GitHub Yapılandırması ───────────────────────────────────────────────

        private string _githubOwner = "";
        public string GithubOwner
        {
            get => _githubOwner;
            set => SetProperty(ref _githubOwner, value);
        }

        private string _githubRepo = "";
        public string GithubRepo
        {
            get => _githubRepo;
            set => SetProperty(ref _githubRepo, value);
        }

        private string _githubToken = "";
        public string GithubToken
        {
            get => _githubToken;
            set => SetProperty(ref _githubToken, value);
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

        // ─── Yerel Sürümü Oku (assembly'den) ────────────────────────────────────

        public void YerelSurumuOku()
        {
            try
            {
                var ver = System.Reflection.Assembly.GetExecutingAssembly()
                    .GetName().Version;
                if (ver != null)
                    LocalVersion = $"{ver.Major}.{ver.Minor}.{ver.Build}";
                else
                    LocalVersion = "3.18.0";
            }
            catch
            {
                LocalVersion = "3.18.0";
            }
        }

        // ─── Firestore Sürüm Kontrolü ────────────────────────────────────────────

        public void FirestoreGuncellemeKontrol(AppGuncelleme doc)
        {
            if (doc == null) return;
            if (string.IsNullOrEmpty(doc.Version) || string.IsNullOrEmpty(doc.DosyaUrl)) return;

            if (!IsNewerVersion(LocalVersion, doc.Version)) return;

            RemoteVersion = doc.Version;
            DownloadUrl = doc.DosyaUrl;
            ReleaseNotes = doc.Notes ?? "";
            UpdateAvailable = true;
        }

        public static bool IsNewerVersion(string current, string remote)
        {
            if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(remote))
                return false;
            try
            {
                var cParts = current.Split('.');
                var rParts = remote.Split('.');
                for (int i = 0; i < Math.Max(cParts.Length, rParts.Length); i++)
                {
                    int cVal = i < cParts.Length && int.TryParse(cParts[i], out var cv) ? cv : 0;
                    int rVal = i < rParts.Length && int.TryParse(rParts[i], out var rv) ? rv : 0;
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

        // ─── GitHub Sürüm Kontrolü ───────────────────────────────────────────────

        private static readonly HttpClient _githubHttp = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
            DefaultRequestHeaders = {
                { Helpers.StringObfuscator.Decode("rYuditW5n52WjA==", 0xF8), "Klyze/3.9" },
                { "Accept", Helpers.StringObfuscator.Decode("mYiIlJGbmYyRl5bXjpac1p+RjJCNmtOSi5eW", 0xF8) }
            }
        };

        public async Task<AppGuncelleme> GithubGuncellemeKontrolAsync()
        {
            if (string.IsNullOrEmpty(GithubOwner) || string.IsNullOrEmpty(GithubRepo))
                return null;

            try
            {
                var url = $"{Helpers.StringObfuscator.Decode("j5OTl5TdyMiGl47JgI6Tj5KFyYSIig==", 0xE7)}/repos/{Uri.EscapeDataString(GithubOwner)}/{Uri.EscapeDataString(GithubRepo)}/releases/latest";
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrEmpty(GithubToken))
                    req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                        Helpers.StringObfuscator.Decode("up2Zip2K", 0xF8), GithubToken);

                var resp = await _githubHttp.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return null;

                var json = JObject.Parse(await resp.Content.ReadAsStringAsync());
                var tagName = json["tag_name"]?.ToString()?.TrimStart('v') ?? "";
                if (string.IsNullOrEmpty(tagName)) return null;

                var assets = json["assets"] as JArray;
                var downloadUrl = assets?.FirstOrDefault()?["browser_download_url"]?.ToString() ?? "";

                return new AppGuncelleme
                {
                    Version = tagName,
                    DosyaUrl = downloadUrl,
                    Notes = json["body"]?.ToString() ?? "",
                    Date = DateTimeOffset.TryParse(json["published_at"]?.ToString(), out var dt) ? dt.ToUnixTimeSeconds() : 0
                };
            }
            catch { return null; }
        }

        public async Task GithubVeFirebaseGuncellemeKontrolAsync()
        {
            // Önce duyuru mesajını çek
            if (App.Firebase != null)
            {
                try
                {
                    var bildirim = await App.Firebase.GetBildirimAsync();
                    if (bildirim != null && bildirim.Aktif)
                    {
                        BildirimBaslik = bildirim.Baslik ?? "";
                        BildirimMesaj = bildirim.Mesaj ?? "";
                    }
                    else
                    {
                        BildirimBaslik = "";
                        BildirimMesaj = "";
                    }
                }
                catch { }
            }

            // 1. Firebase'den sürüm bilgisini al
            if (App.Firebase != null)
            {
                try
                {
                    var doc = await App.Firebase.GuncellemeKontrolFirestoreAsync();
                    if (doc != null && !string.IsNullOrEmpty(doc.Version) && IsNewerVersion(LocalVersion, doc.Version))
                    {
                        RemoteVersion = doc.Version;
                        ReleaseNotes = doc.Notes ?? "";
                        DownloadUrl = !string.IsNullOrEmpty(doc.DirectUrl) ? doc.DirectUrl : (doc.DosyaUrl ?? "");
                        UpdateAvailable = true;
                        return;
                    }
                }
                catch { }
            }

            // 2. GitHub kontrolü devre dışı (manuel güncelleme kullanılıyor)
            // if (!string.IsNullOrEmpty(GithubOwner) && !string.IsNullOrEmpty(GithubRepo))
            // {
            //     try
            //     {
            //         var githubDoc = await GithubGuncellemeKontrolAsync();
            //         if (githubDoc != null && !string.IsNullOrEmpty(githubDoc.Version) && IsNewerVersion(LocalVersion, githubDoc.Version))
            //         {
            //             RemoteVersion = githubDoc.Version;
            //             ReleaseNotes = githubDoc.Notes ?? "";
            //             DownloadUrl = githubDoc.DosyaUrl ?? "";
            //             UpdateAvailable = true;
            //         }
            //     }
            //     catch { }
            // }
        }

        // ─── Güncellemeyi İndir + Kur ────────────────────────────────────────────

        private static string[] AllowedDownloadDomains => new[] {
            Helpers.StringObfuscator.Decode("xMvQx8DD0cfR1s3Qw8XHjMXNzcXOx8PSy9GMwc3P", 0xA2),
            Helpers.StringObfuscator.Decode("0dbN0MPFx4zFzc3FzsfD0svRjMHNzw==", 0xA2),
            Helpers.StringObfuscator.Decode("yc7b2MfFxY/Gx8TD187Wj9DWxsCMxMvQx8DD0cfLzYzBzc8=", 0xA2),
            Helpers.StringObfuscator.Decode("xcvWytfAjMHNzw==", 0xA2),
            Helpers.StringObfuscator.Decode("xcvWytfA19HH0MHNzNbHzNaMwc3P", 0xA2)
        };

        private static bool IsDownloadUrlAllowed(string url)
        {
            if (string.IsNullOrEmpty(url)) return false;
            try
            {
                var uri = new Uri(url);
                return uri.Scheme == "https" &&
                       AllowedDownloadDomains.Any(d => uri.Host.EndsWith(d, StringComparison.OrdinalIgnoreCase));
            }
            catch { return false; }
        }

        [RelayCommand]
        public async Task GuncellemeIndirAsync()
        {
            if (IsDownloading || string.IsNullOrEmpty(DownloadUrl)) return;

            if (!IsDownloadUrlAllowed(DownloadUrl))
            {
                DownloadStatus = "Geçersiz indirme bağlantısı";
                await Task.Delay(3000);
                IsDownloading = false;
                return;
            }

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

                // Aşama 2 — ZIP mi EXE mi kontrol et
                _downloadPhase = 2;
                DownloadStatus = "Kuruluyor...";
                byte[] header = new byte[4];
                using (var fs = new System.IO.FileStream(zipPath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                    fs.Read(header, 0, 4);

                if (header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04)
                {
                    // ZIP dosyası — tüm dosyaları çıkar
                    var extractDir = System.IO.Path.Combine(tempDir, "extracted");
                    if (System.IO.Directory.Exists(extractDir))
                        System.IO.Directory.Delete(extractDir, true);
                    ZipFile.ExtractToDirectory(zipPath, extractDir);

                    _downloadPhase = 3;
                    DownloadStatus = "Yeniden başlatılıyor...";
                    StartUpdateAndExit(extractDir);
                }
                else
                {
                    // Self-contained single-file EXE — sadece EXE'yi değiştir
                    _downloadPhase = 3;
                    DownloadStatus = "Yeniden başlatılıyor...";
                    StartSimpleExeUpdate(zipPath);
                }
            }
            catch (Exception ex)
            {
                DownloadStatus = "İndirme Başarısız";
                _downloadPhase = 0;
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Klyze", "error.log"),
                    $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [UPDATE] {ex.Message}\n");
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
                var proc = System.Diagnostics.Process.Start(psi);
                if (proc != null)
                {
                    Task.Delay(3000).ContinueWith(_ =>
                    {
                        try
                        {
                            if (proc != null && !proc.HasExited)
                                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
                        }
                        catch { }
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
                    System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Klyze", "error.log"),
                    $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [UPDATE START] {ex.Message}\n");
                IsDownloading = false;
            }
        }

        private void StartSimpleExeUpdate(string downloadedExePath)
        {
            try
            {
                var appPath = System.IO.Path.GetDirectoryName(Environment.ProcessPath);
                var exeName = System.IO.Path.GetFileName(Environment.ProcessPath);
                var newExePath = System.IO.Path.Combine(appPath, exeName + ".new");
                System.IO.File.Copy(downloadedExePath, newExePath, true);

                var scriptContent = $"Start-Sleep -Seconds 3; " +
                                   $"Stop-Process -Id {Environment.ProcessId} -Force; " +
                                   $"Move-Item -LiteralPath '{newExePath}' -Destination '{Environment.ProcessPath}' -Force; " +
                                   $"Start-Process -FilePath '{Environment.ProcessPath}'";

                var scriptDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "klyze_update");
                System.IO.Directory.CreateDirectory(scriptDir);
                var scriptPath = System.IO.Path.Combine(scriptDir, "update_exe.ps1");
                System.IO.File.WriteAllText(scriptPath, scriptContent, System.Text.Encoding.Unicode);

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-WindowStyle Hidden -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = true
                };
                var proc = System.Diagnostics.Process.Start(psi);
                if (proc != null)
                {
                    Task.Delay(3000).ContinueWith(_ =>
                    {
                        try
                        {
                            if (proc != null && !proc.HasExited)
                                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
                        }
                        catch { }
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
                    System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Klyze", "error.log"),
                    $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [UPDATE EXE] {ex.Message}\n");
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

$proc = Get-Process -Name ($ExeName -replace '\.exe$','') -ErrorAction SilentlyContinue
if ($proc) {
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

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

            // GitHub yapılandırmasını yükle (varsa, yoksa varsayılan)
            GithubOwner = _configService.Config.GithubOwner;
            GithubRepo = _configService.Config.GithubRepo;
            if (string.IsNullOrEmpty(GithubOwner))
            {
                GithubOwner = "omern2";
                GithubRepo = "klyze-backup";
            }

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
            KlyzeAiVM = new KlyzeAiViewModel();

            // Login VM
            LoginVM = new LoginViewModel(_userService, _henrikApi);
            LoginVM.GirisBasarili += OnGirisBasarili;

            // Giriş durumunu kontrol et
            GirisEkraniGoster = !_userService.GirisYapilmisMi();

            // Yerel sürümü oku (assembly'den) — güncelleme kontrolü için
            YerelSurumuOku();
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
        public KlyzeAiViewModel KlyzeAiVM { get; }
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
            _configService.Config.GithubOwner = GithubOwner;
            _configService.Config.GithubRepo = GithubRepo;
            _configService.Save();
        }

        public void LoadConfig()
        {
            _configService.Load();
            IsDarkMode = _configService.Config.IsDarkMode;
            Language = _configService.Config.Language;
            GithubOwner = _configService.Config.GithubOwner;
            GithubRepo = _configService.Config.GithubRepo;
            AgentVM.LoadFromConfig();
            AfkVM.LoadFromConfig();
            SpamVM.LoadFromConfig();
            HomeVM?.VeriYukle();
        }
    }
}
