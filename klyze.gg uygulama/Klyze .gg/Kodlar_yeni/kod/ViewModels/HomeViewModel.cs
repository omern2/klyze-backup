using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValorantAutoClicker.Models;
using ValorantAutoClicker.Services;

namespace ValorantAutoClicker.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        private readonly UserService _userService;

        public static readonly string[] KeyboardBrands =
            { "Corsair", "Logitech", "Razer", "SteelSeries", "Keychron", "HyperX", "ASUS ROG", "Ducky" };

        public static readonly string[] MonitorBrands =
            { "ASUS", "AOC", "LG", "Samsung", "BenQ", "MSI", "Gigabyte", "Dell" };

        public static readonly string[] MouseBrands =
            { "Logitech", "Razer", "SteelSeries", "Zowie", "Pulsar", "Endgame Gear", "HyperX", "Corsair" };

        private readonly DeviceSelectionModel _selection = new();

        public ObservableCollection<DeviceBrandItem> KeyboardItems { get; }
        public ObservableCollection<DeviceBrandItem> MonitorItems { get; }
        public ObservableCollection<DeviceBrandItem> MouseItems { get; }

        public IRelayCommand<DeviceBrandItem> ToggleDeviceCommand { get; }

        [ObservableProperty]
        private bool _statsYukleniyor = true;

        [ObservableProperty]
        private string _winRateText = "--";

        [ObservableProperty]
        private string _winRateDegisim = "";

        [ObservableProperty]
        private string _winRateOk = "";

        [ObservableProperty]
        private string _winRateRenk = "#888888";

        [ObservableProperty]
        private string _kdText = "--";

        [ObservableProperty]
        private string _kdDegisim = "";

        [ObservableProperty]
        private string _kdOk = "";

        [ObservableProperty]
        private string _kdRenk = "#888888";

        [ObservableProperty]
        private string _adrText = "--";

        [ObservableProperty]
        private string _adrDegisim = "";

        [ObservableProperty]
        private string _adrOk = "";

        [ObservableProperty]
        private string _adrRenk = "#888888";

        [ObservableProperty]
        private string _eloText = "--";

        [ObservableProperty]
        private string _eloDegisim = "";

        [ObservableProperty]
        private string _eloOk = "";

        [ObservableProperty]
        private string _eloRenk = "#888888";

        public HomeViewModel(UserService userService)
        {
            _userService = userService;

            KeyboardItems = new ObservableCollection<DeviceBrandItem>(
                KeyboardBrands.Select(b => new DeviceBrandItem(b)));
            MonitorItems = new ObservableCollection<DeviceBrandItem>(
                MonitorBrands.Select(b => new DeviceBrandItem(b)));
            MouseItems = new ObservableCollection<DeviceBrandItem>(
                MouseBrands.Select(b => new DeviceBrandItem(b)));

            ToggleDeviceCommand = new RelayCommand<DeviceBrandItem>(ToggleDevice);
        }

        public void VeriYukle()
        {
            var profil = _userService.GetProfile();
            if (profil?.GecerliMi == true)
            {
                WinRateText = $"%{profil.KazanmaOrani:F1}";
                KdText = profil.KdOrani.ToString("F2");
                AdrText = profil.Acs.ToString("F0");
                EloText = profil.RutbePuani.ToString();

                WinRateOk = profil.KazanmaOrani >= 50 ? "↑" : "↓";
                WinRateDegisim = profil.KazanmaOrani >= 50
                    ? $"+{profil.KazanmaOrani - 45:F1}%"
                    : $"{profil.KazanmaOrani - 50:F1}%";
                WinRateRenk = profil.KazanmaOrani >= 50 ? "#10B981" : "#FF4655";

                KdOk = profil.KdOrani >= 1.0 ? "↑" : "↓";
                KdDegisim = profil.KdOrani >= 1.0
                    ? $"+{profil.KdOrani - 0.8:F2}"
                    : $"{profil.KdOrani - 1.0:F2}";
                KdRenk = profil.KdOrani >= 1.0 ? "#10B981" : "#FF4655";

                AdrOk = profil.Acs >= 150 ? "↑" : "↓";
                AdrDegisim = profil.Acs >= 150
                    ? $"+{profil.Acs - 130:F0}"
                    : $"{profil.Acs - 150:F0}";
                AdrRenk = profil.Acs >= 150 ? "#10B981" : "#FF4655";

                EloOk = profil.RutbePuani >= 50 ? "↑" : "↓";
                EloDegisim = profil.RutbePuani >= 50
                    ? $"+{profil.RutbePuani - 40}"
                    : $"{profil.RutbePuani - 50}";
                EloRenk = profil.RutbePuani >= 50 ? "#10B981" : "#FF4655";

                StatsYukleniyor = false;
            }
            else
            {
                StatsYukleniyor = false;
            }
        }

        public void ToggleDevice(DeviceBrandItem item)
        {
            if (item == null) return;
            item.IsSelected = !item.IsSelected;
            SyncToModel();
        }

        private void SyncToModel()
        {
            _selection.SelectedKeyboards = KeyboardItems.Where(i => i.IsSelected).Select(i => i.Name).ToList();
            _selection.SelectedMonitors = MonitorItems.Where(i => i.IsSelected).Select(i => i.Name).ToList();
            _selection.SelectedMice = MouseItems.Where(i => i.IsSelected).Select(i => i.Name).ToList();
        }

        public DeviceSelectionModel GetSelection() => _selection;
    }

    public partial class DeviceBrandItem : ObservableObject
    {
        public string Name { get; }

        [ObservableProperty]
        private bool _isSelected;

        public DeviceBrandItem(string name) => Name = name;
    }
}
