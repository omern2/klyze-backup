using System;
using System.Windows;
using ValorantAutoClicker.Services;
using ValorantAutoClicker.ViewModels;
using Microsoft.Extensions.Logging;

namespace ValorantAutoClicker
{
    public partial class App : Application
    {
        public static MainViewModel MainVM { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

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

            // Initialize logging
            LoggingService.Initialize();
            var logger = LoggingService.CreateLogger<App>();

            try
            {
                // Create services
                var configService = new ConfigService();
                var crosshairService = new CrosshairService();
                var clickingService = new ClickingService();
                var afkService = new AfkService(LoggingService.CreateLogger<Services.AfkService>());
                var spamService = new SpamService(LoggingService.CreateLogger<Services.SpamService>());

                // Create main viewmodel
                MainVM = new MainViewModel(
                    configService,
                    crosshairService,
                    clickingService,
                    afkService,
                    spamService);

                logger.LogInformation("Application started successfully.");
            }
            catch (Exception ex)
            {
                // Inner exception zincirini de logla
                var fullMsg = BuildExceptionMessage(ex);
                logger.LogCritical(ex, "Failed to start application.");
                System.IO.File.AppendAllText("error.log", $"\n[STARTUP] {fullMsg}\n");
                MessageBox.Show($"Uygulama başlatılamadı:\n\n{fullMsg}", "Kritik Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
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
