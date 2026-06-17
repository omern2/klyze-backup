using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ValorantAutoClicker.Models;
using ValorantAutoClicker.Services;

namespace ValorantAutoClicker.ViewModels
{
    public partial class PlayerAnalysisViewModel : ObservableObject
    {
        private readonly ConfigService _configService;
        private readonly Action<string> _statusCallback;
        private TrackerApiService _trackerService;
        private CancellationTokenSource _cts;

        // ─── Arama Alanları ──────────────────────────────────────────────────────

        [ObservableProperty] private string _gameName = "";
        [ObservableProperty] private string _tagLine = "";
        [ObservableProperty] private string _gameNameError = "";
        [ObservableProperty] private string _tagLineError = "";

        // ─── Durum ───────────────────────────────────────────────────────────────

        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private bool _hasData;
        [ObservableProperty] private bool _hasError;
        [ObservableProperty] private string _errorMessage = "";
        [ObservableProperty] private bool _fromCache;

        // ─── Profil Verisi ───────────────────────────────────────────────────────

        [ObservableProperty] private TrackerPlayerProfile _profile;

        // ─── Arama Geçmişi ───────────────────────────────────────────────────────

        public ObservableCollection<string> SearchHistory { get; } = new();

        // ─── Maç Listesi (sayfalama) ─────────────────────────────────────────────

        public ObservableCollection<TrackerMatchSummary> Matches { get; } = new();
        [ObservableProperty] private bool _hasMoreMatches;
        private string _nextMatchCursor;

        // ─── Komutlar ────────────────────────────────────────────────────────────

        public IAsyncRelayCommand SearchCommand { get; }
        public IAsyncRelayCommand LoadMoreMatchesCommand { get; }
        public IRelayCommand ClearCommand { get; }
        public IRelayCommand<string> SelectHistoryCommand { get; }
        public IRelayCommand RetryCommand { get; }

        public PlayerAnalysisViewModel(ConfigService configService, Action<string> statusCallback)
        {
            _configService = configService;
            _statusCallback = statusCallback;

            SearchCommand = new AsyncRelayCommand(SearchAsync, CanSearch);
            LoadMoreMatchesCommand = new AsyncRelayCommand(LoadMoreMatchesAsync);
            ClearCommand = new RelayCommand(Clear);
            SelectHistoryCommand = new RelayCommand<string>(SelectHistory);
            RetryCommand = new RelayCommand(Retry);

            InitService();
            LoadSearchHistory();
        }

        // ─── Başlatma ────────────────────────────────────────────────────────────

        private void InitService()
        {
            var key = _configService.Config.TrackerApiKey;
            if (!string.IsNullOrWhiteSpace(key))
            {
                try { _trackerService = new TrackerApiService(key); }
                catch { _trackerService = null; }
            }
        }

        private void LoadSearchHistory()
        {
            SearchHistory.Clear();
            foreach (var item in _configService.Config.SearchHistory ?? new List<string>())
                SearchHistory.Add(item);
        }

        // ─── Doğrulama ───────────────────────────────────────────────────────────

        private bool CanSearch()
        {
            return !IsLoading
                && !string.IsNullOrWhiteSpace(GameName)
                && !string.IsNullOrWhiteSpace(TagLine);
        }

        private bool ValidateInputs()
        {
            var valid = true;
            GameNameError = "";
            TagLineError = "";

            if (string.IsNullOrWhiteSpace(GameName))
            {
                GameNameError = "Kullanıcı adı boş olamaz.";
                valid = false;
            }
            else if (GameName.Length < 3)
            {
                GameNameError = "Kullanıcı adı en az 3 karakter olmalıdır.";
                valid = false;
            }
            else if (GameName.Length > 16)
            {
                GameNameError = "Kullanıcı adı en fazla 16 karakter olabilir.";
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(TagLine))
            {
                TagLineError = "Etiket boş olamaz.";
                valid = false;
            }
            else if (TagLine.Length < 3)
            {
                TagLineError = "Etiket en az 3 karakter olmalıdır.";
                valid = false;
            }
            else if (TagLine.Length > 5)
            {
                TagLineError = "Etiket en fazla 5 karakter olabilir.";
                valid = false;
            }

            return valid;
        }

        // ─── Arama ───────────────────────────────────────────────────────────────

        private async Task SearchAsync()
        {
            if (!ValidateInputs()) return;

            if (_trackerService == null)
            {
                ShowError("Tracker.gg API anahtarı yapılandırılmamış.");
                return;
            }

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            IsLoading = true;
            HasError = false;
            HasData = false;
            ErrorMessage = "";
            Matches.Clear();
            _nextMatchCursor = null;
            HasMoreMatches = false;

            _statusCallback?.Invoke($"{GameName}#{TagLine} aranıyor...");

            try
            {
                var result = await _trackerService.GetPlayerProfileAsync(
                    GameName.Trim(),
                    TagLine.Trim(),
                    _cts.Token);

                Profile = result;
                FromCache = result.FromCache;

                // Maçları listeye ekle
                Matches.Clear();
                foreach (var m in result.RecentMatches)
                    Matches.Add(m);

                HasMoreMatches = result.RecentMatches.Count >= 20;
                HasData = true;

                // Arama geçmişine ekle
                AddToHistory($"{GameName.Trim()}#{TagLine.Trim()}");

                _statusCallback?.Invoke(
                    result.FromCache
                        ? $"{result.RiotId} önbellekten yüklendi."
                        : $"{result.RiotId} başarıyla yüklendi.");
            }
            catch (OperationCanceledException)
            {
                // Kullanıcı iptal etti
            }
            catch (TrackerApiException ex)
            {
                ShowError(ex.Message);
                LoggingService.Error("PlayerAnalysis", ex.Message, ex);
            }
            catch (Exception ex)
            {
                ShowError("Beklenmedik bir hata oluştu. Lütfen tekrar deneyin.");
                LoggingService.Error("PlayerAnalysis", "Beklenmedik hata", ex);
            }
            finally
            {
                IsLoading = false;
                SearchCommand.NotifyCanExecuteChanged();
            }
        }

        private async Task LoadMoreMatchesAsync()
        {
            if (_trackerService == null || string.IsNullOrEmpty(_nextMatchCursor)) return;

            IsLoading = true;
            try
            {
                var more = await _trackerService.GetMoreMatchesAsync(
                    GameName.Trim(),
                    TagLine.Trim(),
                    _nextMatchCursor,
                    _cts?.Token ?? CancellationToken.None);

                foreach (var m in more)
                    Matches.Add(m);

                HasMoreMatches = more.Count >= 20;
            }
            catch (Exception ex)
            {
                ShowError($"Maçlar yüklenemedi: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ─── Yardımcı ────────────────────────────────────────────────────────────

        private void ShowError(string message)
        {
            ErrorMessage = message;
            HasError = true;
            HasData = false;
            _statusCallback?.Invoke(message);

            // 5 saniye sonra otomatik gizle
            Task.Delay(5000).ContinueWith(_ =>
            {
                if (ErrorMessage == message)
                {
                    HasError = false;
                    ErrorMessage = "";
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private void AddToHistory(string riotId)
        {
            var history = _configService.Config.SearchHistory ??= new List<string>();

            history.Remove(riotId);
            history.Insert(0, riotId);
            if (history.Count > 10) history.RemoveAt(history.Count - 1);

            _configService.Save();
            LoadSearchHistory();
        }

        private void SelectHistory(string riotId)
        {
            if (string.IsNullOrEmpty(riotId)) return;
            var parts = riotId.Split('#');
            if (parts.Length == 2)
            {
                GameName = parts[0];
                TagLine = parts[1];
            }
        }

        private void Clear()
        {
            _cts?.Cancel();
            Profile = null;
            HasData = false;
            HasError = false;
            ErrorMessage = "";
            Matches.Clear();
            GameName = "";
            TagLine = "";
            GameNameError = "";
            TagLineError = "";
            _statusCallback?.Invoke("Temizlendi.");
        }

        private void Retry()
        {
            HasError = false;
            ErrorMessage = "";
            if (SearchCommand.CanExecute(null))
                SearchCommand.Execute(null);
        }

        public void RefreshService()
        {
            _trackerService?.Dispose();
            InitService();
        }
    }
}
