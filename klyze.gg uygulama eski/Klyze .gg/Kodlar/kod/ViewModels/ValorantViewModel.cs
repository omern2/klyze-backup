using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
    public partial class ValorantViewModel : ObservableObject
    {
        private readonly ConfigService _configService;
        private ValorantApiService _valorantApiService;
        private readonly Action<string> _statusCallback;

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _statusMessage;
        [ObservableProperty] private ValorantPlayerStats _playerStats;
        [ObservableProperty] private string _errorMessage;
        [ObservableProperty] private bool _hasError;
        [ObservableProperty] private bool _hasData;

        // Commands
        public IRelayCommand FetchStatsCommand { get; }
        public IRelayCommand ClearDataCommand { get; }

        public ValorantViewModel(ConfigService configService, Action<string> statusCallback)
        {
            _configService = configService;
            _statusCallback = statusCallback;
            _playerStats = new ValorantPlayerStats();

            FetchStatsCommand = new AsyncRelayCommand(FetchStatsAsync);
            ClearDataCommand = new RelayCommand(ClearData);

            // Initialize with saved config
            LoadFromConfig();
        }

        public void LoadFromConfig()
        {
            var config = _configService.Config;
            // Initialize API service if we have an API key
            if (!string.IsNullOrEmpty(config.ValorantApiKey))
            {
                _valorantApiService = new ValorantApiService(config.ValorantApiKey);
                _valorantApiService.SetRegion(config.ValorantRegion);
            }
        }

        public void SyncToConfig()
        {
            // Sync ViewModel properties to config if needed
            // For now, we just rely on the config service to have the latest values
        }

        private async Task FetchStatsAsync()
        {
            if (_valorantApiService == null)
            {
                ErrorMessage = "Riot API key not configured. Please set it in Settings.";
                HasError = true;
                _statusCallback?.Invoke(ErrorMessage);
                return;
            }

            var config = _configService.Config;
            if (string.IsNullOrEmpty(config.ValorantUsername) || string.IsNullOrEmpty(config.ValorantTag))
            {
                ErrorMessage = "Please enter your Valorant username and tag.";
                HasError = true;
                _statusCallback?.Invoke(ErrorMessage);
                return;
            }

            IsLoading = true;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = "Fetching your Valorant data...";
            _statusCallback?.Invoke(StatusMessage);

            try
            {
                // Get account info
                var account = await _valorantApiService.GetAccountByNameTag(
                    config.ValorantUsername,
                    config.ValorantTag);

                // Get recent matches
                var matches = await _valorantApiService.GetRecentMatches(account.Puuid, 20);

                // Process the data
                ProcessPlayerData(account, matches);

                HasData = true;
                StatusMessage = $"Successfully loaded data for {account.GameName}#{account.TagLine}";
                _statusCallback?.Invoke(StatusMessage);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to fetch data: {ex.Message}";
                HasError = true;
                _statusCallback?.Invoke(ErrorMessage);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ProcessPlayerData(ValorantAccountResponse account, List<ValorantMatchDetail> matches)
        {
            _playerStats = new ValorantPlayerStats
            {
                Puuid = account.Puuid,
                GameName = account.GameName,
                TagLine = account.TagLine,
                AccountLevel = int.TryParse(account.AccountLevel, out var level) ? level : 0
            };

            if (matches == null || matches.Count == 0)
            {
                _hasData = false;
                return;
            }

            // Process each match
            foreach (var match in matches)
            {
                ProcessMatch(match, account.Puuid);
            }

            // Calculate aggregated stats
            CalculateAggregatedStats();

            // Update HasData flag
            _hasData = _playerStats.TotalMatches > 0;
        }

        private void ProcessMatch(ValorantMatchDetail match, string playerPuuid)
        {
            // Find the player in this match
            var player = match.Info.Players.FirstOrDefault(p => p.Puuid == playerPuuid);
            if (player == null) return;

            // Create match summary
            var matchSummary = new ValorantMatchSummary
            {
                MatchId = match.Metadata.MatchId,
                Map = match.Info.Map,
                Mode = match.Info.Mode,
                GameStart = DateTimeOffset.FromUnixTimeMilliseconds(match.Info.GameStart).DateTime,
                GameLength = TimeSpan.FromSeconds(match.Info.GameLength),
                Win = player.Win,
                Agent = player.Character,
                Kills = player.Kills,
                Deaths = player.Deaths,
                Assists = player.Assists,
                Headshots = player.Headshots,
                Bodyshots = player.Bodyshots,
                Legshots = player.Legshots,
                Acs = player.CombatScore // Assuming CombatScore is equivalent to ACS
            };

            // Add to recent matches (keep only last 10 for display)
            if (_playerStats.RecentMatches.Count >= 10)
            {
                _playerStats.RecentMatches.RemoveAt(0); // Remove oldest
            }
            _playerStats.RecentMatches.Add(matchSummary);

            // Update agent performance
            UpdateAgentPerformance(player.Character, matchSummary);

            // Update map performance
            UpdateMapPerformance(match.Info.Map, matchSummary);

            // Update weekly activity
            UpdateWeeklyActivity(matchSummary.GameStart.DayOfWeek);
        }

        private void UpdateAgentPerformance(string agentName, ValorantMatchSummary matchSummary)
        {
            if (!_playerStats.AgentPerformance.ContainsKey(agentName))
            {
                _playerStats.AgentPerformance[agentName] = new AgentPerformance
                {
                    AgentName = agentName
                };
            }

            var agentPerf = _playerStats.AgentPerformance[agentName];
            agentPerf.MatchesPlayed++;
            if (matchSummary.Win) agentPerf.Wins++;

            // Update running averages
            agentPerf.AverageKda = CalculateRunningAverage(
                agentPerf.AverageKda,
                matchSummary.Kda,
                agentPerf.MatchesPlayed);

            agentPerf.AverageAcs = CalculateRunningAverage(
                agentPerf.AverageAcs,
                matchSummary.Acs,
                agentPerf.MatchesPlayed);
        }

        private void UpdateMapPerformance(string mapName, ValorantMatchSummary matchSummary)
        {
            if (!_playerStats.MapPerformance.ContainsKey(mapName))
            {
                _playerStats.MapPerformance[mapName] = new MapPerformance
                {
                    MapName = mapName
                };
            }

            var mapPerf = _playerStats.MapPerformance[mapName];
            mapPerf.MatchesPlayed++;
            if (matchSummary.Win) mapPerf.Wins++;

            // Update running averages
            mapPerf.AverageKda = CalculateRunningAverage(
                mapPerf.AverageKda,
                matchSummary.Kda,
                mapPerf.MatchesPlayed);

            mapPerf.AverageAcs = CalculateRunningAverage(
                mapPerf.AverageAcs,
                matchSummary.Acs,
                mapPerf.MatchesPlayed);
        }

        private void UpdateWeeklyActivity(DayOfWeek dayOfWeek)
        {
            switch (dayOfWeek)
            {
                case DayOfWeek.Monday: _playerStats.WeeklyActivity.Monday++; break;
                case DayOfWeek.Tuesday: _playerStats.WeeklyActivity.Tuesday++; break;
                case DayOfWeek.Wednesday: _playerStats.WeeklyActivity.Wednesday++; break;
                case DayOfWeek.Thursday: _playerStats.WeeklyActivity.Thursday++; break;
                case DayOfWeek.Friday: _playerStats.WeeklyActivity.Friday++; break;
                case DayOfWeek.Saturday: _playerStats.WeeklyActivity.Saturday++; break;
                case DayOfWeek.Sunday: _playerStats.WeeklyActivity.Sunday++; break;
            }
        }

        private void CalculateAggregatedStats()
        {
            if (_playerStats.RecentMatches.Count == 0) return;

            // Calculate overall KDA and ACS
            _playerStats.TotalMatches = _playerStats.RecentMatches.Count;
            _playerStats.Wins = _playerStats.RecentMatches.Count(m => m.Win);
            _playerStats.AverageKda = _playerStats.RecentMatches.Average(m => m.Kda);
            _playerStats.AverageAcs = _playerStats.RecentMatches.Average(m => m.Acs);

            // Calculate average headshot percentage
            var headshotPercentages = _playerStats.RecentMatches
                .Where(m => m.Kills > 0) // Only consider matches with kills
                .Select(m => m.HeadshotPercentage)
                .ToList();

            if (headshotPercentages.Any())
            {
                _playerStats.AverageHeadshotPercentage = headshotPercentages.Average();
            }
        }

        private double CalculateRunningAverage(double currentAverage, double newValue, int count)
        {
            if (count <= 1) return newValue;
            return ((currentAverage * (count - 1)) + newValue) / count;
        }

        private void ClearData()
        {
            _playerStats = new ValorantPlayerStats();
            HasData = false;
            HasError = false;
            ErrorMessage = string.Empty;
            StatusMessage = string.Empty;
            _statusCallback?.Invoke("Data cleared.");
        }
    }
}