using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace ValorantAutoClicker.Services
{
    public static class LoggingService
    {
        private static ILoggerFactory _factory;
        private static readonly object _fileLock = new object();
        private static string _logFilePath;

        public static void Initialize()
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Klyze", "Logs");
            Directory.CreateDirectory(logDir);
            _logFilePath = Path.Combine(logDir, $"klyze_{DateTime.Now:yyyyMMdd}.log");

            _factory = LoggerFactory.Create(builder =>
            {
                builder.AddFilter("Klyze", LogLevel.Debug)
                       .AddDebug()
                       .AddConsole();
            });

            Info("LoggingService", "Logger initialized. Log file: " + _logFilePath);
        }

        public static ILogger<T> CreateLogger<T>() => _factory.CreateLogger<T>();

        public static void Info(string category, string message) => WriteLog("INFO", category, message);
        public static void Warning(string category, string message) => WriteLog("WARN", category, message);
        public static void Error(string category, string message, Exception ex = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine(message);
            if (ex != null)
            {
                sb.AppendLine($"Exception: {ex.GetType().Name}");
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine($"StackTrace: {ex.StackTrace}");
            }
            WriteLog("ERROR", category, sb.ToString());
        }

        private static void WriteLog(string level, string category, string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] [{category}] {message}";
            System.Diagnostics.Debug.WriteLine(line);
            lock (_fileLock)
            {
                try { File.AppendAllText(_logFilePath, line + Environment.NewLine); }
                catch { /* fail silently - can't log the logger */ }
            }
        }
    }
}
