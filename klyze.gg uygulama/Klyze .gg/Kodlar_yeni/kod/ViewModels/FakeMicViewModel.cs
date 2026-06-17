using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using ValorantAutoClicker.Services;

namespace ValorantAutoClicker.ViewModels
{
    public class PlaylistItem : INotifyPropertyChanged
    {
        public string Path { get; set; }
        public string Name => System.IO.Path.GetFileName(Path);

        private string _displayName;
        public string DisplayName
        {
            get => _displayName ?? System.IO.Path.GetFileNameWithoutExtension(Path);
            set { _displayName = value; OnPropertyChanged(); OnPropertyChanged(nameof(NameShort)); }
        }
        public string NameShort => (DisplayName + System.IO.Path.GetExtension(Path)).Length > 30
            ? (DisplayName + System.IO.Path.GetExtension(Path))[..27] + "..."
            : DisplayName + System.IO.Path.GetExtension(Path);

        private float _volume = 0.8f;
        public float Volume
        {
            get => _volume;
            set { _volume = Math.Clamp(value, 0f, 1f); OnPropertyChanged(); OnPropertyChanged(nameof(VolumePercent)); }
        }
        public int VolumePercent => (int)(Volume * 100);

        private int _hotkeyVk;
        public int HotkeyVk
        {
            get => _hotkeyVk;
            set { _hotkeyVk = value; OnPropertyChanged(); OnPropertyChanged(nameof(HotkeyDisplay)); }
        }
        public string HotkeyDisplay => HotkeyVk > 0 ? SettingsViewModel.VkToName(HotkeyVk) : "Tuş Seç";

        public int SelectedDeviceIndex { get; set; }

        private bool _isNowPlaying;
        public bool IsNowPlaying
        {
            get => _isNowPlaying;
            set { _isNowPlaying = value; OnPropertyChanged(); }
        }

        private bool _isListening;
        public bool IsListening
        {
            get => _isListening;
            set { _isListening = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class FakeMicViewModel : IDisposable
    {
        private FakeMicService _service;

        public ObservableCollection<PlaylistItem> Playlist { get; } = new();
        public ObservableCollection<string> OutputDevices { get; } = new();
        public bool HasItems => Playlist.Any();
        public bool NoItems => !Playlist.Any();

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set { _isPlaying = value; OnPropertyChanged(); }
        }

        private string _statusText = "Hazır";
        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        private bool _isVBCableInstalled;
        public bool IsVBCableInstalled
        {
            get => _isVBCableInstalled;
            set { _isVBCableInstalled = value; OnPropertyChanged(); }
        }
        public int VBCableDeviceIndex { get; private set; } = -1;

        public ICommand AddFilesCommand { get; }
        public ICommand RemoveItemCommand { get; }

        public FakeMicViewModel()
        {
            AddFilesCommand = new RelayCommand(AddFiles);
            RemoveItemCommand = new RelayCommand<PlaylistItem>(RemoveItem);
            Playlist.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(HasItems));
                OnPropertyChanged(nameof(NoItems));
            };
            try
            {
                _service = new FakeMicService();
                _service.PlayingStateChanged += OnPlayingStateChanged;
                _service.SongChanged += OnSongChanged;
                IsVBCableInstalled = _service.IsVBCableInstalled();
                VBCableDeviceIndex = _service.GetVBCableDeviceIndex();
                LoadDevices();
            }
            catch { _service = null; StatusText = "Ses servisi başlatılamadı"; }
        }

        private void LoadDevices()
        {
            OutputDevices.Clear();
            if (_service == null) { OutputDevices.Add("Servis kullanılamıyor"); return; }
            foreach (var d in _service.GetOutputDevices())
                OutputDevices.Add(d);
        }

        private void OnPlayingStateChanged(bool playing)
        {
            IsPlaying = playing;
            StatusText = playing ? "Oynatılıyor..." : "Duraklatıldı";
            if (!playing)
                foreach (var item in Playlist) item.IsNowPlaying = false;
        }

        private void OnSongChanged(string path)
        {
            foreach (var item in Playlist) item.IsNowPlaying = item.Path == path;
        }

        public void AddFiles()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "Ses Dosyaları|*.mp3;*.wav;*.ogg"
            };
            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    if (!Playlist.Any(p => p.Path == file) && File.Exists(file))
                        Playlist.Add(new PlaylistItem { Path = file, SelectedDeviceIndex = Math.Max(0, VBCableDeviceIndex) });
                }
                if (Playlist.Any()) StatusText = $"{Playlist.Count} dosya eklendi";
            }
        }

        public void RemoveItem(PlaylistItem item)
        {
            if (item != null)
            {
                if (item.IsNowPlaying && _service != null) _service.Stop();
                Playlist.Remove(item);
                StatusText = Playlist.Any() ? $"{Playlist.Count} dosya" : "Liste temizlendi";
            }
        }

        public void PlayFile(PlaylistItem item, FakeMicService serviceOverride = null)
        {
            var svc = serviceOverride ?? _service;
            if (svc == null || item == null) return;
            svc.Stop();
            svc.Volume = item.Volume;
            svc.PlayFile(item.Path, item.SelectedDeviceIndex);
        }

        public void Stop()
        {
            _service?.Stop();
        }

        public void ReinitializeService()
        {
            _service?.Dispose();
            _service = null;
            try
            {
                var ns = new FakeMicService();
                ns.PlayingStateChanged += OnPlayingStateChanged;
                ns.SongChanged += OnSongChanged;
                _service = ns;
                IsVBCableInstalled = _service.IsVBCableInstalled();
                VBCableDeviceIndex = _service.GetVBCableDeviceIndex();
                LoadDevices();
            }
            catch { _service = null; StatusText = "Ses servisi başlatılamadı"; }
        }

        public void Dispose() => _service?.Dispose();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
