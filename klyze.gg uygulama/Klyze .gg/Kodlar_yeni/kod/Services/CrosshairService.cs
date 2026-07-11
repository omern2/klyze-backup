using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ValorantAutoClicker.Helpers;
using ValorantAutoClicker.Models;

namespace ValorantAutoClicker.Services
{
    public class CrosshairService
    {
        private static readonly string PROFILES_FILE = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Klyze", "crosshair_profiles.json");
        private readonly Microsoft.Extensions.Logging.ILogger<CrosshairService> _logger;

        public Dictionary<string, CrosshairSettings> Profiles { get; } = new();

        public CrosshairService()
        {
            _logger = LoggingService.CreateLogger<CrosshairService>();
        }

        public void LoadProfiles()
        {
            try
            {
                if (!File.Exists(PROFILES_FILE))
                {
                    CreateDefaultProfiles();
                    return;
                }

                var json = File.ReadAllText(PROFILES_FILE);
                var loaded = SafeJson.Deserialize<Dictionary<string, CrosshairSettings>>(json);
                if (loaded != null)
                {
                    Profiles.Clear();
                    foreach (var p in loaded) Profiles[p.Key] = p.Value;
                    _logger.LogInformation("{Count} crosshair profiles loaded.", Profiles.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load crosshair profiles.");
                CreateDefaultProfiles();
            }
        }

        public void SaveProfiles()
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(PROFILES_FILE);
                if (!string.IsNullOrEmpty(dir))
                    System.IO.Directory.CreateDirectory(dir);
                var json = SafeJson.Serialize(Profiles, Formatting.Indented);
                File.WriteAllText(PROFILES_FILE, json);
                _logger.LogInformation("{Count} crosshair profiles saved.", Profiles.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save crosshair profiles.");
            }
        }

        private void CreateDefaultProfiles()
        {
            Profiles.Clear();
            Profiles["Klasik"] = new CrosshairSettings { InnerLength = 8, InnerThickness = 2, InnerGap = 2 };
            Profiles["Nokta"] = new CrosshairSettings { InnerLength = 4, InnerThickness = 1, InnerGap = 1, CenterDot = true, CenterDotSize = 2 };
            Profiles["Büyük"] = new CrosshairSettings { InnerLength = 12, InnerThickness = 3, InnerGap = 3, OuterLines = true, OuterLength = 6 };
            Profiles["Pro"] = new CrosshairSettings { InnerLength = 6, InnerThickness = 1, InnerGap = 1, OuterLines = true, OuterLength = 3, OuterThickness = 1, OuterGap = 1 };
            Profiles["Minimal"] = new CrosshairSettings { InnerLength = 3, InnerThickness = 1, InnerGap = 0, CenterDot = true, CenterDotSize = 1 };
            SaveProfiles();
        }
    }
}
