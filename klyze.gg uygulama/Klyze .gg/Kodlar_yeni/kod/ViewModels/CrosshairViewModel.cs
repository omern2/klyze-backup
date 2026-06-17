using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValorantAutoClicker.Models;
using ValorantAutoClicker.Services;

namespace ValorantAutoClicker.ViewModels
{
    public partial class CrosshairViewModel : ObservableObject
    {
        private readonly ConfigService _configService;
        private readonly CrosshairService _crosshairService;

        [ObservableProperty] private CrosshairSettings _currentSettings = new();
        [ObservableProperty] private string _selectedProfileName;
        [ObservableProperty] private bool _crosshairEnabled;
        [ObservableProperty] private List<string> _profileNames = new();

        public IRelayCommand<string> SetColorCommand { get; }
        public IRelayCommand SaveProfileCommand { get; }
        public IRelayCommand DeleteProfileCommand { get; }
        public IRelayCommand ImportProfilesCommand { get; }
        public IRelayCommand ExportProfilesCommand { get; }
        public IRelayCommand ToggleCrosshairCommand { get; }

        public CrosshairViewModel(ConfigService configService, CrosshairService crosshairService)
        {
            _configService = configService;
            _crosshairService = crosshairService;

            // Load profiles
            _crosshairService.LoadProfiles();
            RefreshProfileNames();
            if (ProfileNames.Any())
            {
                SelectedProfileName = ProfileNames.First();
                LoadProfile(SelectedProfileName);
            }

            SetColorCommand = new RelayCommand<string>(SetColor);
            SaveProfileCommand = new RelayCommand(SaveProfile);
            DeleteProfileCommand = new RelayCommand(DeleteProfile);
            ImportProfilesCommand = new RelayCommand(ImportProfiles);
            ExportProfilesCommand = new RelayCommand(ExportProfiles);
            ToggleCrosshairCommand = new RelayCommand(ToggleCrosshair);
        }

        public Dictionary<string, CrosshairSettings> GetProfiles() => _crosshairService.Profiles;
        public void SaveProfilesToService() => _crosshairService.SaveProfiles();

        public void RefreshProfileNames()
        {
            ProfileNames = _crosshairService.Profiles.Keys.ToList();
            OnPropertyChanged(nameof(ProfileNames));
        }

        public bool LoadProfileByName(string name)
        {
            if (_crosshairService.Profiles.TryGetValue(name, out var profile))
            {
                CurrentSettings = profile.Clone();
                SelectedProfileName = name;
                return true;
            }
            return false;
        }

        private void LoadProfile(string name)
        {
            if (_crosshairService.Profiles.TryGetValue(name, out var profile))
            {
                CurrentSettings = profile.Clone();
                SelectedProfileName = name;
            }
        }

        private void SetColor(string color)
        {
            CurrentSettings.Color = color;
        }

        private void SaveProfile()
        {
            var name = SelectedProfileName ?? "Yeni Profil";
            _crosshairService.Profiles[name] = CurrentSettings.Clone();
            _crosshairService.SaveProfiles();
            RefreshProfileNames();
        }

        private void DeleteProfile()
        {
            if (SelectedProfileName != null && _crosshairService.Profiles.ContainsKey(SelectedProfileName))
            {
                _crosshairService.Profiles.Remove(SelectedProfileName);
                _crosshairService.SaveProfiles();
                RefreshProfileNames();
                if (ProfileNames.Any()) LoadProfile(ProfileNames.First());
            }
        }

        private void ImportProfiles() { /* TODO: Implement */ }
        private void ExportProfiles() { /* TODO: Implement */ }
        private void ToggleCrosshair()
        {
            CrosshairEnabled = !CrosshairEnabled;
            // TODO: Show/hide overlay
        }
    }
}
