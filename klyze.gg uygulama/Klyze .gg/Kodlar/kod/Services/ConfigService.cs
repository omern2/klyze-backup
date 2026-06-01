using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ValorantAutoClicker.Models;

namespace ValorantAutoClicker.Services
{
    public class ConfigService
    {
        private const string CONFIG_FILE = "config.json";
        private readonly Microsoft.Extensions.Logging.ILogger<ConfigService> _logger;
        public AppConfig Config { get; private set; } = new AppConfig();

        public ConfigService()
        {
            _logger = LoggingService.CreateLogger<ConfigService>();
        }

        public void Load()
        {
            try
            {
                if (!File.Exists(CONFIG_FILE))
                {
                    _logger.LogInformation("Config file not found, using defaults.");
                    return;
                }

                var json = File.ReadAllText(CONFIG_FILE);
                var cfg = JsonConvert.DeserializeObject<AppConfig>(json);
                if (cfg != null)
                {
                    Config = cfg;
                    _logger.LogInformation("Config loaded successfully.");
                }
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
                var json = JsonConvert.SerializeObject(Config, Formatting.Indented);
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
