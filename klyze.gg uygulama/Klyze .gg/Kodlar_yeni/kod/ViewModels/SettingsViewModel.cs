using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValorantAutoClicker.Models;
using ValorantAutoClicker.Services;

namespace ValorantAutoClicker.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ConfigService _configService;

        [ObservableProperty] private int _hotkeyAgent = 2;
        [ObservableProperty] private int _hotkeyAfk = 0;
        [ObservableProperty] private int _hotkeySpam = 1;
        [ObservableProperty] private int _hotkeyFakeMic = 0x78;

        public IRelayCommand<string> SetLanguageCommand { get; }
        public IRelayCommand<bool> SetThemeCommand { get; }
        public IRelayCommand SaveCommand { get; }

        public SettingsViewModel(ConfigService configService)
        {
            _configService = configService;
            LoadFromConfig();
            SaveCommand = new RelayCommand(Save);
            SetLanguageCommand = new RelayCommand<string>(SetLanguage);
            SetThemeCommand = new RelayCommand<bool>(SetTheme);
        }

        public void LoadFromConfig()
        {
            HotkeyAgent = _configService.Config.HotkeyAgent;
            HotkeyAfk = _configService.Config.HotkeyAfk;
            HotkeySpam = _configService.Config.HotkeySpam;
            int hk = _configService.Config.HotkeyFakeMic;
            if (hk >= 0 && hk < 10) hk = new[] { 0x75, 0x76, 0x77, 0x78 }[Math.Min(hk, 3)];
            HotkeyFakeMic = hk;
            _configService.Config.HotkeyFakeMic = hk;
        }

        public static string VkToName(int vk)
        {
            if (vk >= 0x70 && vk <= 0x7F) return $"F{vk - 0x70}";
            if (vk >= 0x30 && vk <= 0x39) return $"{vk - 0x30}";
            if (vk >= 0x41 && vk <= 0x5A) return $"{(char)vk}";
            switch (vk)
            {
                case 0x08: return "Backspace"; case 0x09: return "Tab";
                case 0x0D: return "Enter"; case 0x10: return "Shift";
                case 0x11: return "Ctrl"; case 0x12: return "Alt";
                case 0x1B: return "Esc"; case 0x20: return "Space";
                case 0x2E: return "Delete"; case 0x25: return "←";
                case 0x27: return "→"; case 0x26: return "↑";
                case 0x28: return "↓";
                default: return $"Tuş {vk}";
            }
        }

        public void SyncToConfig()
        {
            _configService.Config.HotkeyAgent = HotkeyAgent;
            _configService.Config.HotkeyAfk = HotkeyAfk;
            _configService.Config.HotkeySpam = HotkeySpam;
            _configService.Config.HotkeyFakeMic = HotkeyFakeMic;
        }

        private void Save()
        {
            SyncToConfig();
            _configService.Save();
        }

        private void SetLanguage(string lang)
        {
            _configService.Config.Language = lang;
            _configService.Save();
        }

        private void SetTheme(bool isDark)
        {
            _configService.Config.IsDarkMode = isDark;
            _configService.Save();
        }
    }
}
