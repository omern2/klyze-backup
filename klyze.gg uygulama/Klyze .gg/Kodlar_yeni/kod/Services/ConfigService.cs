using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.Logging;
using ValorantAutoClicker.Helpers;
using ValorantAutoClicker.Models;

namespace ValorantAutoClicker.Services
{
    public class ConfigService
    {
        public static ConfigService Instance { get; private set; }

        private static readonly string CONFIG_DIR = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Klyze");
        private static readonly string CONFIG_FILE = System.IO.Path.Combine(CONFIG_DIR, "config.json");
        private readonly Microsoft.Extensions.Logging.ILogger<ConfigService> _logger;
        public AppConfig Config { get; private set; } = new AppConfig();

        public ConfigService()
        {
            Instance = this;
            _logger = LoggingService.CreateLogger<ConfigService>();
            System.IO.Directory.CreateDirectory(CONFIG_DIR);
        }

        public void Load()
        {
            try
            {
                if (File.Exists(CONFIG_FILE))
                {
                    var json = File.ReadAllText(CONFIG_FILE);
                    var cfg = SafeJson.Deserialize<AppConfig>(json);
                    if (cfg != null)
                    {
                        Config = cfg;
                    }
                }

                _logger.LogInformation("Config loaded successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load config.");
            }
        }

        public void Save()
        {
            try
            {
                var json = SafeJson.Serialize(Config, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(CONFIG_FILE, json);
                _logger.LogInformation("Config saved.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save config.");
            }
        }
    }
}
