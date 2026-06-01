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
        }

        public void SyncToConfig()
        {
            _configService.Config.HotkeyAgent = HotkeyAgent;
            _configService.Config.HotkeyAfk = HotkeyAfk;
            _configService.Config.HotkeySpam = HotkeySpam;
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
