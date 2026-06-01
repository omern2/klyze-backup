using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValorantAutoClicker.Services;
using ValorantAutoClicker.Models;

namespace ValorantAutoClicker.ViewModels
{
    public partial class AfkViewModel : ObservableObject
    {
        private readonly ConfigService _configService;
        private readonly AfkService _afkService;

        [ObservableProperty] private int _duration = 60;
        [ObservableProperty] private int _interval = 2;
        [ObservableProperty] private bool _isInfinite;
        [ObservableProperty] private bool _isRunning;
        [ObservableProperty] private bool _forward = true;
        [ObservableProperty] private bool _backward;
        [ObservableProperty] private bool _right;
        [ObservableProperty] private bool _left;
        [ObservableProperty] private bool _jump = true;
        [ObservableProperty] private bool _shoot;

        public IRelayCommand ToggleCommand { get; }

        public AfkViewModel(ConfigService configService, AfkService afkService)
        {
            _configService = configService;
            _afkService = afkService;
            Duration = 60;
            Interval = 2;

            ToggleCommand = new AsyncRelayCommand(ToggleAfk);
            _afkService.RunningStateChanged += (r) => IsRunning = r;
        }

        public void LoadFromConfig()
        {
            // Afk settings are not stored in config currently; could be added
        }

        public void SyncToConfig()
        {
            // Could save afk settings to config
        }

        private async Task ToggleAfk()
        {
            if (IsRunning)
            {
                _afkService.Stop();
            }
            else
            {
                await _afkService.StartAsync(
                    Duration, Interval, IsInfinite,
                    Forward, Backward, Right, Left, Jump, Shoot);
            }
        }
    }
}
