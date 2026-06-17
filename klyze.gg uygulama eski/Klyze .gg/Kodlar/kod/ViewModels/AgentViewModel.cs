using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValorantAutoClicker.Models;
using ValorantAutoClicker.Services;

namespace ValorantAutoClicker.ViewModels
{
    public partial class AgentViewModel : ObservableObject
    {
        private readonly ConfigService _configService;
        private readonly ClickingService _clickingService;
        private readonly Action<string> _statusCallback;

        [ObservableProperty] private int _speed = 31;
        [ObservableProperty] private string _duration = "5";
        [ObservableProperty] private bool _isRunning;
        [ObservableProperty] private bool _hasPositions;

        public List<Point> Positions => _configService.Config.Positions;

        public IRelayCommand AddPositionCommand { get; }
        public IRelayCommand DeletePositionCommand { get; }
        public IRelayCommand ToggleCommand { get; }

        public AgentViewModel(ConfigService configService, ClickingService clickingService, Action<string> statusCallback)
        {
            _configService = configService;
            _clickingService = clickingService;
            _statusCallback = statusCallback;

            Speed = _configService.Config.Speed;
            Duration = _configService.Config.Duration;
            HasPositions = Positions.Count > 0;

            AddPositionCommand = new RelayCommand(AddPosition);
            DeletePositionCommand = new RelayCommand<int?>(DeletePosition);
            ToggleCommand = new AsyncRelayCommand(ToggleClicking);

            _clickingService.RunningStateChanged += (r) => IsRunning = r;
            _clickingService.StatusChanged += (s) => _statusCallback?.Invoke(s);
        }

        public void LoadFromConfig()
        {
            Speed = _configService.Config.Speed;
            Duration = _configService.Config.Duration;
            HasPositions = Positions.Count > 0;
        }

        public void SyncToConfig()
        {
            _configService.Config.Speed = Speed;
            _configService.Config.Duration = Duration;
        }

        private void AddPosition()
        {
            _statusCallback?.Invoke("3 saniye icinde tiklayin...");
            // We need to get cursor position after 3 seconds.
            // Since we can't await in a RelayCommand easily, we'll fire and forget.
            Task.Run(async () =>
            {
                await Task.Delay(3000);
                if (GetCursorPos(out POINT p))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Positions.Add(new Point(p.X, p.Y));
                        HasPositions = Positions.Count > 0;
                        _statusCallback?.Invoke($"Pozisyon {Positions.Count} eklendi!");
                        _configService.Save();
                    });
                }
            });
        }

        private void DeletePosition(int? index)
        {
            if (index.HasValue && index.Value >= 0 && index.Value < Positions.Count)
            {
                Positions.RemoveAt(index.Value);
                HasPositions = Positions.Count > 0;
                _statusCallback?.Invoke($"Silindi. Kalan: {Positions.Count}");
                if (Positions.Count == 0) HasPositions = false;
                _configService.Save();
            }
        }

        private async Task ToggleClicking()
        {
            if (IsRunning)
            {
                _clickingService.Stop();
            }
            else
            {
                if (Positions.Count == 0)
                {
                    _statusCallback?.Invoke("Pozisyon ekleyin!");
                    return;
                }
                await _clickingService.StartAsync(Positions, Speed);
            }
        }

        // Win32
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }
    }
}
