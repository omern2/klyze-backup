using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ValorantAutoClicker.Models
{
    public class AppConfig
    {
        public List<Point> Positions { get; set; } = new();
        public int Speed { get; set; } = 31;
        public string Duration { get; set; } = "5";
        public bool IsDarkMode { get; set; } = true;
        public string Language { get; set; } = "TR";
        public int HotkeyAgent { get; set; } = 2;
        public int HotkeyAfk { get; set; } = 0;
        public int HotkeySpam { get; set; } = 1;

        // Valorant Analysis Properties
        public string ValorantUsername { get; set; } = "";
        public string ValorantTag { get; set; } = "";
        public string ValorantRegion { get; set; } = "eu"; // Default to europe
        public string ValorantApiKey { get; set; } = "";

        // Tracker.gg API
        public string TrackerApiKey { get; set; } = "cf01159e-1c36-4e15-802a-edc75a20af35";

        // Arama geçmişi (son 10)
        public List<string> SearchHistory { get; set; } = new();
    }
}
