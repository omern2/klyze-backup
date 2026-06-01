using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValorantAutoClicker.Services;
using ValorantAutoClicker.Models;

namespace ValorantAutoClicker.ViewModels
{
    public partial class SpamViewModel : ObservableObject
    {
        private readonly ConfigService _configService;
        private readonly SpamService _spamService;

        [ObservableProperty] private string _spamText = "AutoClicker";
        [ObservableProperty] private int _count = 10;
        [ObservableProperty] private int _speed = 1;
        [ObservableProperty] private bool _isRunning;

        public IRelayCommand ToggleCommand { get; }

        public SpamViewModel(ConfigService configService, SpamService spamService)
        {
            _configService = configService;
            _spamService = spamService;

            ToggleCommand = new AsyncRelayCommand(ToggleSpam);
            _spamService.RunningStateChanged += (r) => IsRunning = r;
        }

        public void LoadFromConfig()
        {
            // Spam settings not in config currently
        }

        public void SyncToConfig()
        {
            // Could save spam settings
        }

        private async Task ToggleSpam()
        {
            if (IsRunning)
            {
                _spamService.Stop();
            }
            else
            {
                await _spamService.StartAsync(SpamText, Count, Speed);
            }
        }
    }
}
