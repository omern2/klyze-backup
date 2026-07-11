using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using ValorantAutoClicker.Services;
using ValorantAutoClicker.ViewModels;
using Microsoft.Extensions.Logging;

namespace ValorantAutoClicker
{
    public partial class App : Application
    {
        public static MainViewModel MainVM { get; private set; }
        public static FirebaseService Firebase { get; private set; }
        public static GoogleAuthService GoogleAuth { get; private set; }
        private static System.Threading.Timer _firebaseTimer;
        private static ILogger _appLogger;
        protected override void OnStartup(StartupEventArgs e)
        {
            // Tüm unhandled exception'ları yakala, sadece logla, kapatma
            DispatcherUnhandledException += (s, ex) =>
            {
                LogExceptionSilent(ex.Exception);
                ex.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                LogExceptionSilent(ex.ExceptionObject as Exception);
            };

            try
            {
                // Initialize logging
                LoggingService.Initialize();
                _appLogger = LoggingService.CreateLogger<App>();

                // Create services (synchronous)
                var configService = new ConfigService();
                var crosshairService = new CrosshairService();
                var clickingService = new ClickingService();
                var afkService = new AfkService(LoggingService.CreateLogger<Services.AfkService>());
                var spamService = new SpamService(LoggingService.CreateLogger<Services.SpamService>());

                // Create MainVM BEFORE window creation (MainWindow constructor accesses App.MainVM)
                MainVM = new MainViewModel(
                    configService,
                    crosshairService,
                    clickingService,
                    afkService,
                    spamService);

                // Firebase config fetch — arka planda çalıştır, window hemen açılsın
                Firebase = new FirebaseService();
                GoogleAuth = new GoogleAuthService();
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var apiKeyOk = await Firebase.BootstrapApiKeyAsync().ConfigureAwait(false);
                        if (!apiKeyOk) return;
                        var authOk = await Firebase.AnonimGirisAsync().ConfigureAwait(false);
                        if (!authOk) return;
                        await ApiKeyProvider.LoadFromFirebaseAsync(Firebase).ConfigureAwait(false);

                        // Google OAuth credentials'ları yükle
                        await GoogleAuth.LoadCredentialsFromFirebaseAsync(Firebase).ConfigureAwait(false);

                        // AI API key'lerini ViewModel'e aktar (Groq multi-key)
                        var groqKeys = new List<string>();
                        if (!string.IsNullOrEmpty(ApiKeyProvider.GroqAiKey))
                            groqKeys.Add(ApiKeyProvider.GroqAiKey);
                        if (!string.IsNullOrEmpty(ApiKeyProvider.GroqAiKey2))
                            groqKeys.Add(ApiKeyProvider.GroqAiKey2);
                        if (groqKeys.Count > 0)
                        {
                            Dispatcher.Invoke(() => MainVM?.KlyzeAiVM?.GroqKeyleriniGuncelle(groqKeys));
                        }

                        // Tavily web search API key
                        var tavilyKey = ApiKeyProvider.TavilyApiKey;
                        if (!string.IsNullOrEmpty(tavilyKey))
                        {
                            Dispatcher.Invoke(() => MainVM?.KlyzeAiVM?.TavilyKeyGuncelle(tavilyKey));
                        }

                        // LoginVM'e GoogleAuth'u enjekte et (UI thread'de)
                        Dispatcher.Invoke(() => MainVM?.LoginVM?.SetGoogleAuth(GoogleAuth));

                        _appLogger?.LogInformation("Firebase init + Google Auth loaded.");
                    }
                    catch (Exception fEx)
                    {
                        _appLogger?.LogWarning(fEx, "Firebase init failed — continuing without online features.");
                    }
                });

                // Extract embedded rank icons for single-file deployment
                ExtractRankIcons();

                // Now create the window via base startup
                base.OnStartup(e);

                // İlk güncelleme kontrolü 5 saniye sonra başlasın
                _ = Task.Delay(5000).ContinueWith(async _ =>
                {
                    try { await FirestoreGuncellemeKontrolAsync(); }
                    catch { }
                });

                // Keepalive — her 5 saniyede bir güncelleme kontrolü
                _firebaseTimer = new System.Threading.Timer(
                    async _ => await FirebaseKeepaliveAsync(),
                    null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

                _appLogger.LogInformation("Application started successfully.");
            }
            catch (Exception ex)
            {
                LogExceptionSilent(ex);
                if (_appLogger != null)
                    _appLogger.LogCritical(ex, "Failed to start application.");
                MessageBox.Show("Uygulama başlatılamadı. Lütfen daha sonra tekrar deneyin.", "Kritik Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private async Task FirebaseKeepaliveAsync()
        {
            try
            {
                if (Firebase == null) return;
                await FirestoreGuncellemeKontrolAsync();
            }
            catch
            {
                // Ignore transient errors, next ping will catch
            }
        }

        private async Task FirestoreGuncellemeKontrolAsync()
        {
            try
            {
                if (MainVM == null) return;

                // Önce Firebase + GitHub kombinasyon kontrolü
                await MainVM.GithubVeFirebaseGuncellemeKontrolAsync();

                // Firebase yoksa doğrudan GitHub kontrolü de yapılmış oldu
            }
            catch
            {
                // Sessizce vazgeç, UI değişmez
            }
        }

        private static void ExtractRankIcons()
        {
            try
            {
                var targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ranklar");
                if (Directory.Exists(targetDir) && Directory.GetFiles(targetDir, "*.png").Length >= 20)
                    return;

                Directory.CreateDirectory(targetDir);
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var prefix = "ValorantAutoClicker.ranklar.";
                foreach (var name in assembly.GetManifestResourceNames())
                {
                    if (!name.StartsWith(prefix)) continue;
                    var fileName = name.Substring(prefix.Length);
                    var targetPath = Path.Combine(targetDir, fileName);
                    if (!File.Exists(targetPath))
                    {
                        using var stream = assembly.GetManifestResourceStream(name);
                        if (stream != null)
                            using (var fileStream = File.Create(targetPath))
                                stream.CopyTo(fileStream);
                    }
                }
            }
            catch { }
        }

        private static void LogExceptionSilent(Exception ex)
        {
            if (ex == null) return;
            try
            {
                var logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Klyze");
                Directory.CreateDirectory(logDir);
                File.AppendAllText(Path.Combine(logDir, "error.log"),
                    $"\n[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n");
            }
            catch { }
        }
    }
}
