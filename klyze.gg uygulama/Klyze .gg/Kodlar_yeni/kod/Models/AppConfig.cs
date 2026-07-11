using System;
using System.Collections.Generic;
using System.Windows;
using Newtonsoft.Json;
using ValorantAutoClicker.Helpers;

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
        public int HotkeyFakeMic { get; set; } = 0x78;

        public string ValorantUsername { get; set; } = "";
        public string ValorantTag { get; set; } = "";
        public string ValorantRegion { get; set; } = "eu";

        [JsonProperty("ValorantApiKey")]
        private string ValorantApiKeyEncrypted { get; set; } = "";

        [JsonIgnore]
        public string ValorantApiKey
        {
            get => SecureStorage.Decrypt(ValorantApiKeyEncrypted);
            set => ValorantApiKeyEncrypted = SecureStorage.Encrypt(value ?? "");
        }

        public List<string> SearchHistory { get; set; } = new();

        public string GithubOwner { get; set; } = "";
        public string GithubRepo { get; set; } = "";
    }
}
