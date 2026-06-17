using System;
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
        private static System.Threading.Timer _firebaseTimer;
        private static ILogger _appLogger;
        private static string _sonGuncellemeVersiyonu = "";
        private const string Surum = "3.0.0";

        protected override void OnStartup(StartupEventArgs e)
        {
            // Tüm unhandled exception'ları yakala
            DispatcherUnhandledException += (s, ex) =>
            {
                var msg = BuildExceptionMessage(ex.Exception);
                System.IO.File.AppendAllText("error.log", $"\n[DISPATCHER] {msg}\n");
                MessageBox.Show($"Hata:\n\n{msg}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
                Shutdown();
            };

            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                var msg = BuildExceptionMessage(ex.ExceptionObject as Exception);
                System.IO.File.AppendAllText("error.log", $"\n[APPDOMAIN] {msg}\n");
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

                // Now create the window via base startup
                base.OnStartup(e);

                // Firebase config fetch + keepalive (async, non-blocking)
                _ = FirebaseBaslatAsync();

                _appLogger.LogInformation("Application started successfully.");
            }
            catch (Exception ex)
            {
                var fullMsg = BuildExceptionMessage(ex);
                if (_appLogger != null)
                    _appLogger.LogCritical(ex, "Failed to start application.");
                System.IO.File.AppendAllText("error.log", $"\n[STARTUP] {fullMsg}\n");
                MessageBox.Show($"Uygulama başlatılamadı:\n\n{fullMsg}", "Kritik Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        private async Task FirebaseBaslatAsync()
        {
            try
            {
                Firebase = new FirebaseService();
                var authOk = await Firebase.AnonimGirisAsync();
                if (!authOk)
                {
                    _appLogger.LogWarning("Firebase auth failed — shutting down.");
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("Veritabanına bağlanılamadı. Uygulama kapatılıyor.", "Bağlantı Hatası",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        Shutdown();
                    });
                    return;
                }

                _appLogger.LogInformation("Firebase anonymous auth succeeded.");

                // API key'lerini yükle
                await ApiKeyProvider.LoadFromFirebaseAsync(Firebase);

                // Güncelleme kontrolü
                await GuncellemeKontrolEtAsync();

                // Keepalive — her 30 saniyede bir ping
                _firebaseTimer = new System.Threading.Timer(
                    async _ => await FirebaseKeepaliveAsync(),
                    null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            }
            catch (Exception ex)
            {
                _appLogger.LogError(ex, "Firebase başlatma hatası.");
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Veritabanı bağlantısı kurulamadı. Uygulama kapatılıyor.", "Bağlantı Hatası",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                });
            }
        }

        private async Task FirebaseKeepaliveAsync()
        {
            try
            {
                if (Firebase == null) return;
                var ok = await Firebase.PingAsync();
                if (!ok)
                {
                    _appLogger.LogWarning("Firebase ping failed — shutting down.");
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("Veritabanı bağlantısı koptu. Uygulama kapatılıyor.", "Bağlantı Hatası",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        Shutdown();
                    });
                    return;
                }

                // Her ping'de güncelleme kontrolü (açıkken yayınlanan bildirimler için)
                await GuncellemeKontrolEtAsync();
            }
            catch
            {
                // Ignore transient errors, next ping will catch
            }
        }

        private async Task GuncellemeKontrolEtAsync()
        {
            try
            {
                var guncelleme = await Firebase.GuncellemeKontrolAsync();
                if (guncelleme == null || MainVM == null) return;

                if (guncelleme.Version == _sonGuncellemeVersiyonu) return;
                _sonGuncellemeVersiyonu = guncelleme.Version;

                if (string.IsNullOrEmpty(guncelleme.Version) || string.IsNullOrEmpty(guncelleme.DosyaUrl))
                    return;

                var simdiki = new Version(Surum);
                var firebaseVer = new Version(guncelleme.Version.TrimStart('v', 'V'));
                if (firebaseVer <= simdiki) return;

                _appLogger.LogInformation("Yeni sürüm bulundu: v{0} (mevcut: v{1})", guncelleme.Version, Surum);

                Dispatcher.Invoke(() => MainVM.AddGuncellemeBildirimi(guncelleme));
            }
            catch (Exception ex)
            {
                _appLogger.LogError(ex, "Güncelleme kontrolü hatası.");
            }
        }

        private static string BuildExceptionMessage(Exception ex)
        {
            var sb = new System.Text.StringBuilder();
            var current = ex;
            int depth = 0;
            while (current != null && depth < 5)
            {
                sb.AppendLine($"[{depth}] {current.GetType().Name}: {current.Message}");
                current = current.InnerException;
                depth++;
            }
            return sb.ToString();
        }
    }
}
