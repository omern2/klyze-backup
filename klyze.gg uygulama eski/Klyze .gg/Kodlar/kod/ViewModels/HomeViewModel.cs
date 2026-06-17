using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValorantAutoClicker.Models;

namespace ValorantAutoClicker.ViewModels
{
    public partial class HomeViewModel : ObservableObject
    {
        // Klavye markaları
        public static readonly string[] KeyboardBrands =
            { "Corsair", "Logitech", "Razer", "SteelSeries", "Keychron", "HyperX", "ASUS ROG", "Ducky" };

        // Monitör markaları
        public static readonly string[] MonitorBrands =
            { "ASUS", "AOC", "LG", "Samsung", "BenQ", "MSI", "Gigabyte", "Dell" };

        // Fare markaları
        public static readonly string[] MouseBrands =
            { "Logitech", "Razer", "SteelSeries", "Zowie", "Pulsar", "Endgame Gear", "HyperX", "Corsair" };

        private readonly DeviceSelectionModel _selection = new();

        public ObservableCollection<DeviceBrandItem> KeyboardItems { get; }
        public ObservableCollection<DeviceBrandItem> MonitorItems { get; }
        public ObservableCollection<DeviceBrandItem> MouseItems { get; }

        public IRelayCommand<DeviceBrandItem> ToggleDeviceCommand { get; }

        public HomeViewModel()
        {
            KeyboardItems = new ObservableCollection<DeviceBrandItem>(
                KeyboardBrands.Select(b => new DeviceBrandItem(b)));
            MonitorItems = new ObservableCollection<DeviceBrandItem>(
                MonitorBrands.Select(b => new DeviceBrandItem(b)));
            MouseItems = new ObservableCollection<DeviceBrandItem>(
                MouseBrands.Select(b => new DeviceBrandItem(b)));

            ToggleDeviceCommand = new RelayCommand<DeviceBrandItem>(ToggleDevice);
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
